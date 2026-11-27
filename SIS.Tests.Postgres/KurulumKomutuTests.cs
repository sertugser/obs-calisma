using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SIS.Infrastructure.Persistence.Kurulum;

namespace SIS.Tests.Postgres;

// Adım 3.1 kabul testi: boş (yalnız migration uygulanmış) veritabanında `setup` komutu.
// Uygulama Production ortamında açılır; açılıştaki Development seed'i çalışmaz, veriyi yalnız kurulum yazar.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class KurulumKomutuTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Kurulum_YoneticiGirerVeMenuyuGorur()
    {
        await using var uygulama = new SisUygulamasi(fixture.BaglantiCumlesi);
        const string email = "yonetici@example.edu";
        var password = RastgeleParola();

        Assert.Equal(0, await KurAsync(uygulama, email, password));

        using var istemci = uygulama.CreateClient();
        var giris = await istemci.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, giris.StatusCode);
        using var girisGovde = JsonDocument.Parse(await giris.Content.ReadAsStringAsync());
        var token = girisGovde.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrEmpty(token));

        istemci.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var menu = await istemci.GetAsync("/api/me/menu");
        Assert.Equal(HttpStatusCode.OK, menu.StatusCode);
        using var menuGovde = JsonDocument.Parse(await menu.Content.ReadAsStringAsync());
        Assert.True(menuGovde.RootElement.GetArrayLength() > 0);
    }

    [Fact]
    public async Task IkinciKurulum_HicbirTablonunSatirSayisiniDegistirmez_IkinciYoneticiOlusmaz()
    {
        await using var uygulama = new SisUygulamasi(fixture.BaglantiCumlesi);

        Assert.Equal(0, await KurAsync(uygulama, "yonetici@example.edu", RastgeleParola()));
        var ilk = await SatirSayilariAsync();
        // Migration'ın kendi yazdığı satırlardan fazlası olmalı: sistem verisini kurulum yazdı.
        foreach (var tablo in new[] { "Permissions", "SystemConfigs" })
            Assert.True(ilk[tablo] > fixture.MigrationVerisi.GetValueOrDefault(tablo), tablo);
        foreach (var tablo in new[] { "AspNetRoles", "MenuModules", "MenuItems", "RolePermissions", "WebModuleSettings", "PermissionBundles", "DocumentTypes" })
            Assert.True(ilk[tablo] > 0, tablo);

        // Başka bir e-postayla ikinci kez: yönetici zaten var, yeni hesap açılmaz.
        Assert.Equal(0, await KurAsync(uygulama, "ikinci@example.edu", RastgeleParola()));
        Assert.Equal(ilk, await SatirSayilariAsync());

        await using var db = fixture.YeniContext();
        Assert.Equal(["yonetici@example.edu"], await db.Users.Select(u => u.Email!).ToListAsync());
    }

    [Fact]
    public async Task ParolaYoksa_KurulumDurur_KullaniciOlusmaz()
    {
        await using var uygulama = new SisUygulamasi(fixture.BaglantiCumlesi);

        Assert.Equal(1, await KurAsync(uygulama, "yonetici@example.edu", parola: null));

        await using var db = fixture.YeniContext();
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task DemoBayragiYoksa_OrnekVeriYazilmaz()
    {
        await using var uygulama = new SisUygulamasi(fixture.BaglantiCumlesi);

        Assert.Equal(0, await KurAsync(uygulama, "yonetici@example.edu", RastgeleParola()));

        await using var db = fixture.YeniContext();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(0, await db.Students.CountAsync());
        Assert.Equal(0, await db.Instructors.CountAsync());
        Assert.Equal(0, await db.Faculties.CountAsync());
        Assert.Equal(0, await db.Courses.CountAsync());
        Assert.Equal(0, await db.CourseRegistrations.CountAsync());
    }

    [Fact]
    public async Task KurumVerisiYalnizKurumBayragiyla_CekirdekteBesAcmaNedeni()
    {
        await using var uygulama = new SisUygulamasi(fixture.BaglantiCumlesi);

        Assert.Equal(0, await KurAsync(uygulama, "yonetici@example.edu", RastgeleParola()));
        await using (var db = fixture.YeniContext())
        {
            Assert.Equal(5, await db.CurriculumCourseOpeningReasons.CountAsync());
            Assert.Equal(0, await db.Classrooms.CountAsync());
        }

        Assert.Equal(0, await KurulumCalistirici.CalistirAsync(
            uygulama.Services,
            new KurulumSecenekleri(DemoVerisi: false, "yonetici@example.edu", RastgeleParola(), Kurum: "yerel"),
            NullLogger.Instance));

        await using var kontrol = fixture.YeniContext();
        Assert.True(await kontrol.Faculties.AnyAsync());
        Assert.True(await kontrol.Courses.AnyAsync());
        Assert.True(await kontrol.Classrooms.AnyAsync());
        Assert.Equal(0, await kontrol.Students.CountAsync());
        Assert.Equal(21, await kontrol.CurriculumCourseOpeningReasons.CountAsync());
    }

    [Fact]
    public async Task BilinmeyenKurum_KurulumDurur()
    {
        await using var uygulama = new SisUygulamasi(fixture.BaglantiCumlesi);

        Assert.Equal(1, await KurulumCalistirici.CalistirAsync(
            uygulama.Services,
            new KurulumSecenekleri(DemoVerisi: false, "yonetici@example.edu", RastgeleParola(), Kurum: "olmayan"),
            NullLogger.Instance));
    }

    [Fact]
    public void Argumanlar_KurumAdiniOkur()
    {
        var s = KurulumSecenekleri.Oku(["setup", "--kurum", "yerel", "--seed-demo"], _ => null);
        Assert.Equal(("yerel", true), (s.Kurum, s.DemoVerisi));
        Assert.Null(KurulumSecenekleri.Oku(["setup"], _ => null).Kurum);
    }

    private static Task<int> KurAsync(SisUygulamasi uygulama, string eposta, string? parola) =>
        KurulumCalistirici.CalistirAsync(
            uygulama.Services,
            new KurulumSecenekleri(DemoVerisi: false, eposta, parola),
            NullLogger.Instance);

    // Denetim günlüğü her yazımda büyüdüğü için karşılaştırmanın dışında.
    private async Task<SortedDictionary<string, long>> SatirSayilariAsync()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        var sayilar = await PostgresFixture.SatirSayilariAsync(baglanti);
        sayilar.Remove("AuditLogs");
        return new SortedDictionary<string, long>(sayilar);
    }

    // Büyük harf, küçük harf, rakam ve simge içeren rastgele parola.
    private static string RastgeleParola() => "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private sealed class SisUygulamasi(string baglantiCumlesi) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
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
