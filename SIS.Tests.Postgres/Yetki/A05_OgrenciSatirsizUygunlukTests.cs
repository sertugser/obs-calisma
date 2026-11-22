using System.Net;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// A-05 ek: öğrenci rolünde olup öğrenci satırı olmayan kullanıcı Z; Y'nin öğrenci satırı var. Karşı örnek (satırı olan öğrenci,
// kendi kimliğiyle 200): A05_A06_UygunlukUcuTests.A05_OgrenciRoluKendiUygunlukUcunda200Alir_YalnizKendiSatirlariDoner.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class A05_OgrenciSatirsizUygunlukTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "a05s-ogrenci-z@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A05_OgrenciSatiriOlmayanOgrenciRoluUygunlukUcunda403Alir_GovdeBos()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("Student");
        var zKullanici = await kurucu.KullaniciAsync(Email, "Student");
        var yKullanici = await kurucu.KullaniciAsync("a05s-ogrenci-y@ornek.test", "Student");
        var veri = await KapsamVeriKurucu.KurAsync(fixture, zKullanici, yKullanici);
        var y = await veri.OgrenciAsync(yKullanici);
        Assert.Distinct([zKullanici, yKullanici, y, .. veri.DersIdleri]);
        await using (var db = fixture.YeniContext())
            Assert.False(db.Students.Any(s => s.UserId == zKullanici));
        var istemci = await kurucu.IstemciAsync(Email);

        var yanit = await istemci.GetAsync($"/api/eligibility/student/{y}");

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        Assert.Equal(string.Empty, await yanit.Content.ReadAsStringAsync());
    }
}
