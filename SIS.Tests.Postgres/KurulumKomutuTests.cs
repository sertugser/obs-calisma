using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using SIS.Infrastructure.Persistence;
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
            Assert.Equal(
                new[] { "Internship", "Other", "Periodic", "Seminar", "Thesis" },
                (await db.CurriculumCourseOpeningReasons.Select(r => r.Code).ToListAsync()).Order(StringComparer.Ordinal));
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

        // Ad veritabanına dokunmadan önce reddedilir.
        await using var db = fixture.YeniContext();
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Faculties.CountAsync());
    }

    [Fact]
    public async Task YoneticininSildigiKurumAcmaNedeni_KurumYenidenYazilincaGeriGelmez()
    {
        await using var uygulama = new SisUygulamasi(fixture.BaglantiCumlesi);
        var secenek = new KurulumSecenekleri(DemoVerisi: false, "yonetici@example.edu", RastgeleParola(), Kurum: "yerel");
        Assert.Equal(0, await KurulumCalistirici.CalistirAsync(uygulama.Services, secenek, NullLogger.Instance));
        await using (var db = fixture.YeniContext())
        {
            db.CurriculumCourseOpeningReasons.Remove(await db.CurriculumCourseOpeningReasons.SingleAsync(r => r.Code == "Hobby"));
            await db.SaveChangesAsync();
        }

        Assert.Equal(0, await KurulumCalistirici.CalistirAsync(uygulama.Services, secenek, NullLogger.Instance));

        await using var kontrol = fixture.YeniContext();
        Assert.False(await kontrol.CurriculumCourseOpeningReasons.AnyAsync(r => r.Code == "Hobby"));
    }

    [Fact]
    public void Argumanlar_KurumAdiniOkur()
    {
        var s = KurulumSecenekleri.Oku(["setup", "--kurum", "yerel", "--seed-demo"], _ => null);
        Assert.Equal(("yerel", true), (s.Kurum, s.DemoVerisi));
        Assert.Null(KurulumSecenekleri.Oku(["setup"], _ => null).Kurum);
        Assert.Throws<ArgumentException>(() => KurulumSecenekleri.Oku(["setup", "--kurum"], _ => null));
        Assert.Throws<ArgumentException>(() => KurulumSecenekleri.Oku(["setup", "--kurum", "--seed-demo"], _ => null));
    }

    [Fact]
    public async Task OrnekVeri_DonemlerVerilenGuneGore_NotlarGecmisDonemlerde()
    {
        await using var uygulama = new SisUygulamasi(fixture.BaglantiCumlesi);
        var gun = new DateTime(2026, 11, 26, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(0, await KurulumCalistirici.CalistirAsync(
            uygulama.Services,
            new KurulumSecenekleri(DemoVerisi: true, "yonetici@example.edu", RastgeleParola(), DemoTarihi: gun),
            NullLogger.Instance));

        await using var db = fixture.YeniContext();
        var etkin = await db.AcademicTerms.Where(t => t.IsActivated).ToListAsync();
        Assert.Equal((2026, TermType.Fall), (Assert.Single(etkin).Year, etkin[0].TermType));
        // Güz 2022'den Güz 2026'ya dokuz dönem (gelecekteki Bahar 2027 yok); migration'ın yaz dönemi ayrıca durur.
        Assert.Equal(9, await db.AcademicTerms.CountAsync(t => t.TermType != TermType.Summer));
        Assert.False(await db.AcademicTerms.AnyAsync(t => t.TermStart > gun));
        Assert.Equal(0, await db.AcademicTerms.CountAsync(t => t.TermType != TermType.Summer && t.AcademicCalendar == null));
        // Madde 45: notlu kayıtlar geçmiş dönemlerde, etkin dönemde notlu kayıt yok.
        Assert.True(await db.CourseRegistrations.AnyAsync(r => r.Grade != null));
        Assert.False(await db.CourseRegistrations.AnyAsync(r => r.Grade != null && r.CourseSection.TermId == etkin[0].Id));

        // Hoca ataması ve program: etkin dönemin her şubesinin hocası ve en az bir program satırı var;
        // aynı hoca aynı zaman diliminde iki şubede değil.
        var subeler = await db.CourseSections.Where(s => s.TermId == etkin[0].Id).Include(s => s.SectionSchedules).ToListAsync();
        Assert.NotEmpty(subeler);
        Assert.All(subeler, s => { Assert.NotNull(s.InstructorId); Assert.NotEmpty(s.SectionSchedules); });
        var hocaDilim = subeler.SelectMany(s => s.SectionSchedules.Select(ss => (s.InstructorId, ss.TimeSlotId))).ToList();
        Assert.Equal(hocaDilim.Count, hocaDilim.Distinct().Count());
    }

    [Fact]
    public void OrnekVeriDonemSecimi_DonemArasindaVeYazinDoğru()
    {
        // Saf tarih mantığı: veritabanı gerekmez, bu yüzden demo seed'i koşturulmaz.
        Assert.Equal((2025, TermType.Fall), SeedData.GuncelDemoDonemi(new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal((2025, TermType.Spring), SeedData.GuncelDemoDonemi(new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal((2025, TermType.Spring), SeedData.GuncelDemoDonemi(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal((2026, TermType.Fall), SeedData.GuncelDemoDonemi(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)));
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
