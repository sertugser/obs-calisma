using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Enums;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// Dersten çekilme isteğinde kayıt sahipliği: X giriş yapan öğrenci, Y başka öğrenci; ikisinin de aynı dönemde iki etkin kaydı var.
// Çekilme modülü web ayarıyla açık; dönem tarihleri ve saat sonuca girmez. İki test arasındaki tek fark istenen kayıt kimliği.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class CekilmeKayitSahiplikTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "cekilme-ogrenci-x@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Cekilme_BaskaOgrencininKaydiyla400Alir_KayitBulunamadi_HicbirKayitDegismez()
    {
        var (istemci, k) = await KurAsync();
        var once = await DurumAsync(k);

        var yanit = await istemci.PostAsync($"/api/registrations/{k.Y[0]}/withdraw", null);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.False(govde.RootElement.GetProperty("success").GetBoolean());
        var hata = Assert.Single(govde.RootElement.GetProperty("validationResult").GetProperty("errors").EnumerateArray());
        Assert.Equal("RegistrationExists", hata.GetProperty("rule").GetString());
        Assert.Equal("Kayit bulunamadi.", hata.GetProperty("message").GetString());
        Assert.Equal(once, await DurumAsync(k));
    }

    // Karşı örnek: aynı istek, öğrencinin kendi kaydıyla.
    [Fact]
    public async Task Cekilme_KendiKaydiyla200Alir_YalnizHedefKayitCekilir()
    {
        var (istemci, k) = await KurAsync();

        var yanit = await istemci.PostAsync($"/api/registrations/{k.X[0]}/withdraw", null);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var etkin = (RegistrationStatus.Active, (GradeCode?)null, 0);
        Assert.Equal(
            ($"{(RegistrationStatus.Withdrawn, GradeCode.W, 1)} {etkin} {etkin} {etkin}", "1 0", "1 2"),
            await DurumAsync(k));
    }

    /// <summary>Kayıtlar (X, Y sırasıyla: durum, not, çekilme sayısı), öğrencilerin çekilme sayıları ve şube mevcutları; yeni context'le.</summary>
    private async Task<(string Kayitlar, string Ogrenciler, string Subeler)> DurumAsync(Kurulum k)
    {
        await using var db = fixture.YeniContext();
        var kayitlar = await db.CourseRegistrations.AsNoTracking().Where(r => k.X.Contains(r.Id) || k.Y.Contains(r.Id)).ToListAsync();
        var sirali = k.X.Concat(k.Y).Select(id => kayitlar.Single(r => r.Id == id))
            .Select(r => (r.Status, r.Grade, r.CourseWithdrawalCount).ToString());
        var ogrenciler = new List<int>();
        foreach (var id in new[] { k.XOgrenci, k.YOgrenci })
            ogrenciler.Add(await db.Students.Where(s => s.Id == id).Select(s => s.WithdrawalCount).SingleAsync());
        var subeler = new List<int>();
        foreach (var id in k.Subeler)
            subeler.Add(await db.CourseSections.Where(s => s.Id == id).Select(s => s.EnrolledCount).SingleAsync());
        return (string.Join(" ", sirali), string.Join(" ", ogrenciler), string.Join(" ", subeler));
    }

    /// <summary>
    /// Öğrenci rolünde çekilme izni; X ve Y öğrencileri, iki şube, her öğrencinin iki etkin kaydı.
    /// Kullanıcı, öğrenci, ders, şube ve kayıt kimlikleri ayrık (ön koşul); başlangıçta her kayıt etkin, şube mevcutları 2.
    /// </summary>
    private async Task<(HttpClient, Kurulum)> KurAsync()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("Student", "menu.ders-kayit.withdraw");
        var xKullanici = await kurucu.KullaniciAsync(Email, "Student");
        var yKullanici = await kurucu.KullaniciAsync("cekilme-ogrenci-y@ornek.test", "Student");
        var veri = await KapsamVeriKurucu.KurAsync(fixture, xKullanici, yKullanici);
        var x = await veri.OgrenciAsync(xKullanici);
        var y = await veri.OgrenciAsync(yKullanici);
        var (subeler, kayitlar) = await veri.CekilmeKurAsync(x, y);
        var k = new Kurulum(x, y, subeler, kayitlar[0], kayitlar[1]);
        Assert.Distinct([xKullanici, yKullanici, x, y, .. veri.DersIdleri, .. subeler, .. k.X, .. k.Y]);
        var etkin = (RegistrationStatus.Active, (GradeCode?)null, 0);
        Assert.Equal(($"{etkin} {etkin} {etkin} {etkin}", "0 0", "2 2"), await DurumAsync(k));
        return (await kurucu.IstemciAsync(Email), k);
    }

    private sealed record Kurulum(int XOgrenci, int YOgrenci, int[] Subeler, int[] X, int[] Y);
}
