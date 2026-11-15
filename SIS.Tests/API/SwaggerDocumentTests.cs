using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SIS.API.Controllers;

namespace SIS.Tests.API;

/// <summary>
/// /swagger/v1/swagger.json belgesinin hatasız üretildiğini korur. Orval istemcisi bu belgeden üretilir;
/// tek bir eylemin imzası belgeyi bozarsa tüm belge HTTP 500 döner.
/// Program.cs çalıştırılmaz (veritabanı, gizli anahtar ve tohumlama gerekmez); gerçek controller'lar
/// süreç içi, loopback'teki bir Kestrel sunucusunda gerçek SwaggerMiddleware ile sunulur.
/// </summary>
public class SwaggerDocumentTests : IClassFixture<SwaggerDocumentTests.SwaggerHostFixture>
{
    private const string ImportPreviewPath = "/api/schedule-excel/import-preview";

    private readonly SwaggerHostFixture _fixture;

    public SwaggerDocumentTests(SwaggerHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Swagger_Json_Is_Generated_Without_Error()
    {
        using var document = ParseDocument();

        var pathCount = document.RootElement.GetProperty("paths").EnumerateObject().Count();
        pathCount.Should().BeGreaterThan(0, "belge en az bir yol içermeli");
    }

    [Fact]
    public void Schedule_Import_Preview_Is_Documented_As_Multipart_File_Upload()
    {
        using var document = ParseDocument();

        var operation = Navigate(document.RootElement, "paths", ImportPreviewPath, "post");

        var file = Navigate(operation, "requestBody", "content", "multipart/form-data", "schema", "properties", "file");
        file.GetProperty("type").GetString().Should().Be("string");
        file.GetProperty("format").GetString().Should().Be("binary");

        // termId ve facultyId sorgudan okunmaya devam eder; belgeye de sorgu parametresi olarak yansımalı.
        var queryParameters = operation.GetProperty("parameters").EnumerateArray()
            .Where(parameter => parameter.GetProperty("in").GetString() == "query")
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToList();
        queryParameters.Should().BeEquivalentTo(["termId", "facultyId"]);
    }

    private JsonDocument ParseDocument()
    {
        _fixture.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "swagger.json üretimi hata vermemeli. Yanıt gövdesi (ilk 2000 karakter): {0}",
            _fixture.Body.Length <= 2000 ? _fixture.Body : _fixture.Body[..2000]);

        return JsonDocument.Parse(_fixture.Body);
    }

    private static JsonElement Navigate(JsonElement element, params string[] path)
    {
        var current = element;
        var visited = new List<string>();
        foreach (var segment in path)
        {
            visited.Add(segment);
            if (!current.TryGetProperty(segment, out current))
                throw new Xunit.Sdk.XunitException($"swagger.json içinde şu yol yok: {string.Join(" > ", visited)}");
        }

        return current;
    }

    public sealed class SwaggerHostFixture : IAsyncLifetime
    {
        private WebApplication? _app;
        private string? _contentRoot;

        public HttpStatusCode StatusCode { get; private set; }

        public string Body { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            // Boş içerik kökü: appsettings*.json yüklenmesin.
            _contentRoot = Directory.CreateTempSubdirectory("swagger-test-").FullName;

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development",
                ContentRootPath = _contentRoot,
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();

            // Program.cs:55-59 ve :123-127 ile aynı tutulur (Swagger belgesini etkileyen ayarlar).
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(ScheduleExcelController).Assembly)
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
            });

            _app = builder.Build();
            _app.UseSwagger();

            await _app.StartAsync();

            var address = _app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.First();

            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var response = await client.GetAsync("/swagger/v1/swagger.json");
            StatusCode = response.StatusCode;
            Body = await response.Content.ReadAsStringAsync();
        }

        public async Task DisposeAsync()
        {
            if (_app is not null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }

            if (_contentRoot is not null && Directory.Exists(_contentRoot))
                Directory.Delete(_contentRoot, recursive: true);
        }
    }
}
