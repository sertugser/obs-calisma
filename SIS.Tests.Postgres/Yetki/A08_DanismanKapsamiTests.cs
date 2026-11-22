using System.Net;
using System.Text.Json;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// A-08: danışman rolü; kendi danışanı X ile başka danışmanın öğrencisi Y aynı kurulumda. Danışman onayı ve reddi için
// danışman eşlemesi mevcut AdvisorApprovalServiceTests (Approve_FailsForWrongAdvisor, Reject_FailsForWrongAdvisor) ile sabit.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class A08_DanismanKapsamiTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "a08-danisman@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A08_DanismanListesi200Alir_YalnizKendiDanisaniDoner()
    {
        var (istemci, x, _, _) = await KurAsync();

        var yanit = await istemci.GetAsync("/api/advisor/students");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal([x], govde.RootElement.EnumerateArray().Select(s => s.GetProperty("studentId").GetInt32()).ToList());
    }

    [Fact]
    public async Task A08_DanismanBaskaDanismaninOgrencisininKartinda403Alir_GovdeBos()
    {
        var (istemci, _, y, _) = await KurAsync();

        var yanit = await istemci.GetAsync($"/api/admin/students/{y}");

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        Assert.Equal(string.Empty, await yanit.Content.ReadAsStringAsync());
    }

    // Karşı örnek: aynı istek, danışmanın kendi danışanıyla.
    [Fact]
    public async Task A08_DanismanKendiDanisaninKartinda200Alir_OgrenciKimligiDoner()
    {
        var (istemci, x, _, xNumara) = await KurAsync();

        var yanit = await istemci.GetAsync($"/api/admin/students/{x}");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal(x, govde.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(xNumara, govde.RootElement.GetProperty("studentNumber").GetString());
    }

    /// <summary>
    /// Danışman D1 (giriş yapan; liste ve öğrenci kartı izinleri) ve D2 aynı bölümde; X'in danışmanı D1, Y'nin D2.
    /// Kullanıcı, öğretim elemanı, öğrenci ve ders kimlikleri ayrık (ön koşul).
    /// </summary>
    private async Task<(HttpClient, int X, int Y, string XNumara)> KurAsync()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("AcademicAdvisor", "menu.ders-kayit-onay.view", "menu.ogrenci-rehberi.view");
        var d1Kullanici = await kurucu.KullaniciAsync(Email, "AcademicAdvisor");
        var d2Kullanici = await kurucu.KullaniciAsync("a08-danisman-2@ornek.test", "AcademicAdvisor");
        var xKullanici = await kurucu.KullaniciAsync("a08-ogrenci-x@ornek.test");
        var yKullanici = await kurucu.KullaniciAsync("a08-ogrenci-y@ornek.test");
        var veri = await KapsamVeriKurucu.KurAsync(fixture, d1Kullanici, d2Kullanici, xKullanici, yKullanici);
        var d1 = await veri.OgretimElemaniAsync(d1Kullanici);
        var d2 = await veri.OgretimElemaniAsync(d2Kullanici);
        var x = await veri.OgrenciAsync(xKullanici, d1);
        var y = await veri.OgrenciAsync(yKullanici, d2);
        Assert.Distinct([d1Kullanici, d2Kullanici, xKullanici, yKullanici, d1, d2, x, y, .. veri.DersIdleri]);
        await using var db = fixture.YeniContext();
        var xNumara = db.Students.Where(s => s.Id == x).Select(s => s.StudentNumber).Single();
        return (await kurucu.IstemciAsync(Email), x, y, xNumara);
    }
}
