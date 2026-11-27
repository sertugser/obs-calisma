using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SIS.Infrastructure.Persistence.Kurulum;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Postgres;

// A-01: boş (yalnız migration uygulanmış) veritabanında API açılışı veri yazmaz (Adım 3.8); veri
// `setup` ile gelir ve setup'ın yöneticisi giriş yapıp menüyü görür.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class KurulumVeGirisTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Acilis_HicbirOrtamdaVeriYazmaz(string ortam)
    {
        var once = await SatirSayilariAsync();
        await using var uygulama = new SisUygulamasi(ortam, fixture.BaglantiCumlesi);
        using var istemci = uygulama.CreateClient();

        var giris = await istemci.PostAsJsonAsync("/api/auth/login", new { email = "yok@ornek.test", password = RastgeleParola() });
        Assert.Equal(HttpStatusCode.Unauthorized, giris.StatusCode);
        var sonra = await SatirSayilariAsync();
        Assert.Empty(sonra.Where(s => once.GetValueOrDefault(s.Key) != s.Value).Select(s => $"{s.Key}: {once.GetValueOrDefault(s.Key)} -> {s.Value}"));
    }

    [Fact]
    public async Task Development_SetupYoneticisiGirerVeMenuyuGorur()
    {
        await using var uygulama = new SisUygulamasi("Development", fixture.BaglantiCumlesi);
        const string email = "yonetici@example.edu";
        var password = RastgeleParola();
        Assert.Equal(0, await KurulumCalistirici.CalistirAsync(
            uygulama.Services, new KurulumSecenekleri(DemoVerisi: false, email, password), NullLogger.Instance));

        using var istemci = uygulama.CreateClient();
        var giris = await istemci.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, giris.StatusCode);
        using var girisGovde = JsonDocument.Parse(await giris.Content.ReadAsStringAsync());
        Assert.True(girisGovde.RootElement.GetProperty("success").GetBoolean());
        var token = girisGovde.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrEmpty(token));

        istemci.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var menu = await istemci.GetAsync("/api/me/menu");
        Assert.Equal(HttpStatusCode.OK, menu.StatusCode);
        using var menuGovde = JsonDocument.Parse(await menu.Content.ReadAsStringAsync());
        Assert.True(menuGovde.RootElement.GetArrayLength() > 0);
    }

    // Sayımın dışında: denetim günlüğü ve değerlendirme dönemleri. Değerlendirme dönemini açılışta
    // seed değil arka plan işi (EvaluationPeriodOpenerJob) açar; migration'ın eklediği yaz dönemi için
    // açtığı kapanmış dönem Adım 2 kapanış listesinin 41 numaralı maddesi (bugünkü davranış).
    private async Task<SortedDictionary<string, long>> SatirSayilariAsync()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        var sayilar = await PostgresFixture.SatirSayilariAsync(baglanti);
        sayilar.Remove("AuditLogs");
        sayilar.Remove("EvaluationPeriods");
        return new SortedDictionary<string, long>(sayilar);
    }

    // Büyük harf, küçük harf, rakam ve simge içeren rastgele parola.
    private static string RastgeleParola() => "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    // Program.cs olduğu gibi çalışır; yapılandırma yalnız test değerleriyle ezilir.
    private sealed class SisUygulamasi(string ortam, string baglantiCumlesi) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(ortam);
            builder.UseSetting("ConnectionStrings:DefaultConnection", baglantiCumlesi);
            builder.UseSetting("Jwt:Key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("Jwt:Issuer", "sis-test");
            builder.UseSetting("Jwt:Audience", "sis-test");
            builder.UseSetting("Jwt:ExpiresInMinutes", "30");
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            builder.UseSetting("AllowedHosts", "localhost");
        }
    }
}
