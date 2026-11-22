using System.Net;
using System.Text.Json;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// A-05 ve A-06: öğrenci rolünün uygunluk uçları. İki etkin öğrenci (X giriş yapan, Y başka öğrenci) aynı program ve müfredatta.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class A05_A06_UygunlukUcuTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string XEmail = "a05-ogrenci-x@ornek.test";
    private const string YEmail = "a05-ogrenci-y@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A05_OgrenciRoluBaskaOgrencininUygunlukUcunda403Alir_GovdeBos()
    {
        var (istemci, _, y, _) = await KurAsync();

        var yanit = await istemci.GetAsync($"/api/eligibility/student/{y}");

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        Assert.Equal(string.Empty, await yanit.Content.ReadAsStringAsync());
    }

    // Karşı örnek: aynı istek, öğrencinin kendi kimliğiyle.
    [Fact]
    public async Task A05_OgrenciRoluKendiUygunlukUcunda200Alir_YalnizKendiSatirlariDoner()
    {
        var (istemci, x, _, dersler) = await KurAsync();

        var yanit = await istemci.GetAsync($"/api/eligibility/student/{x}");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var satirlar = govde.RootElement.EnumerateArray().ToList();
        Assert.All(satirlar, s => Assert.Equal(x, s.GetProperty("studentId").GetInt32()));
        Assert.Equal(dersler.Order().ToList(), satirlar.Select(s => s.GetProperty("courseId").GetInt32()).Order().ToList());
    }

    // Bilinen hata A-06
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task A06_OgrenciRoluDersUygunlukUcunda200Alir_IkiOgrenciDoner()
    {
        var (istemci, x, y, dersler) = await KurAsync();

        var yanit = await istemci.GetAsync($"/api/eligibility/course/{dersler[0]}");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var kok = govde.RootElement;
        Assert.Equal(dersler[0], kok.GetProperty("courseId").GetInt32());
        var ogrenciler = kok.GetProperty("eligibleStudents").EnumerateArray()
            .Concat(kok.GetProperty("ineligibleStudents").EnumerateArray())
            .Select(s => s.GetProperty("studentId").GetInt32()).Order().ToList();
        Assert.Equal(new[] { x, y }.Order().ToList(), ogrenciler);
        Assert.Equal(2, kok.GetProperty("totalEligible").GetInt32() + kok.GetProperty("totalIneligible").GetInt32());
    }

    // Karşı örnek: aynı istek, jetonsuz.
    [Fact]
    public async Task A06_JetonsuzDersUygunlukIstegi401Alir()
    {
        var (_, _, _, dersler) = await KurAsync();

        var yanit = await uygulama.Al(fixture.BaglantiCumlesi).CreateClient().GetAsync($"/api/eligibility/course/{dersler[0]}");

        Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
    }

    /// <summary>Öğrenci rolü izin satırı olmadan; X ve Y öğrencileri. Kullanıcı, öğrenci ve ders kimlikleri ayrık (ön koşul).</summary>
    private async Task<(HttpClient, int X, int Y, int[] Dersler)> KurAsync()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("Student");
        var xKullanici = await kurucu.KullaniciAsync(XEmail, "Student");
        var yKullanici = await kurucu.KullaniciAsync(YEmail, "Student");
        var veri = await KapsamVeriKurucu.KurAsync(fixture, xKullanici, yKullanici);
        var x = await veri.OgrenciAsync(xKullanici);
        var y = await veri.OgrenciAsync(yKullanici);
        Assert.Distinct([xKullanici, yKullanici, x, y, .. veri.DersIdleri]);
        return (await kurucu.IstemciAsync(XEmail), x, y, veri.DersIdleri);
    }
}
