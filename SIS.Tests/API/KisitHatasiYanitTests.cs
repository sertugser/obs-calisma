using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SIS.API.Middleware;

namespace SIS.Tests.API;

// Adım 3.10: işlem sonunda düşen ertelemeli kısıt DbUpdateException'a sarılmadan PostgresException olarak
// gelir; ara katman bunu 409 ve Türkçe iletiyle döner, ham PostgreSQL iletisini client'a göndermez.
public sealed class KisitHatasiYanitTests
{
    [Theory]
    [InlineData("23P01", 409)] // dışlama kısıtı (tek etkin dönem)
    [InlineData("23514", 409)] // CHECK kısıtı
    [InlineData("40001", 500)] // başka bir veritabanı hatası
    public async Task PostgresHatasi_HamIletiGonderilmez(string sqlState, int beklenenDurum)
    {
        var hata = new PostgresException("conflicting key value violates exclusion constraint \"EX_AcademicTerms_TekEtkinDonem\"",
            "ERROR", "ERROR", sqlState);
        var ara = new GlobalExceptionMiddleware(_ => throw hata, NullLogger<GlobalExceptionMiddleware>.Instance, new Ortam());
        var baglam = new DefaultHttpContext();
        baglam.Response.Body = new MemoryStream();
        // Denetim kaydı yazımı 5xx'te denenir; servis sağlayıcı yoksa sessizce atlanır.
        baglam.RequestServices = new ServiceCollection().BuildServiceProvider();

        await ara.InvokeAsync(baglam);

        Assert.Equal(beklenenDurum, baglam.Response.StatusCode);
        baglam.Response.Body.Position = 0;
        using var govde = await JsonDocument.ParseAsync(baglam.Response.Body);
        var ileti = govde.RootElement.GetProperty("error").GetString()!;
        Assert.DoesNotContain("EX_AcademicTerms_TekEtkinDonem", ileti);
        Assert.DoesNotContain("violates", ileti);
    }

    private sealed class Ortam : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "SIS.Tests";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
