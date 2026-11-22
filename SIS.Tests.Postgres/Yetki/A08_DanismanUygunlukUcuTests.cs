using System.Net;
using System.Text.Json;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// A-08 (4): danışman rolü, öğrenci uygunluk ucu. D1'in danışanı X, D2'nin danışanı Y; giriş yapan D1, rolde izin satırı yok.
// Öğrenci rolünün başka öğrencinin kimliğiyle aldığı 403, A05_A06_UygunlukUcuTests'te sabit.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class A08_DanismanUygunlukUcuTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "a08u-danisman@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A08_DanismanRoluDanisaniOlmayanOgrencininUygunlukUcunda200Alir_OgrencininSatirlariDoner()
    {
        var (kurucu, y, dersler) = await KurAsync();
        var istemci = await kurucu.IstemciAsync(Email);

        var yanit = await istemci.GetAsync($"/api/eligibility/student/{y}");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var satirlar = govde.RootElement.EnumerateArray().ToList();
        Assert.All(satirlar, s => Assert.Equal(y, s.GetProperty("studentId").GetInt32()));
        Assert.Equal(dersler.Order().ToList(), satirlar.Select(s => s.GetProperty("courseId").GetInt32()).Order().ToList());
    }

    // Karşı örnek: aynı uç ve aynı kimlik, jetonsuz.
    [Fact]
    public async Task A08_JetonsuzOgrenciUygunlukIstegi401Alir()
    {
        var (_, y, _) = await KurAsync();

        var yanit = await uygulama.Al(fixture.BaglantiCumlesi).CreateClient().GetAsync($"/api/eligibility/student/{y}");

        Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
    }

    /// <summary>D1 ve D2 aynı bölümde; X'in danışmanı D1, Y'nin D2. Kullanıcı, öğretim elemanı, öğrenci ve ders kimlikleri ayrık (ön koşul).</summary>
    private async Task<(YetkiKurucu, int Y, int[] Dersler)> KurAsync()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("AcademicAdvisor");
        var d1Kullanici = await kurucu.KullaniciAsync(Email, "AcademicAdvisor");
        var d2Kullanici = await kurucu.KullaniciAsync("a08u-danisman-2@ornek.test", "AcademicAdvisor");
        var xKullanici = await kurucu.KullaniciAsync("a08u-ogrenci-x@ornek.test");
        var yKullanici = await kurucu.KullaniciAsync("a08u-ogrenci-y@ornek.test");
        var veri = await KapsamVeriKurucu.KurAsync(fixture, d1Kullanici, d2Kullanici, xKullanici, yKullanici);
        var d1 = await veri.OgretimElemaniAsync(d1Kullanici);
        var d2 = await veri.OgretimElemaniAsync(d2Kullanici);
        var x = await veri.OgrenciAsync(xKullanici, d1);
        var y = await veri.OgrenciAsync(yKullanici, d2);
        Assert.Distinct([d1Kullanici, d2Kullanici, xKullanici, yKullanici, d1, d2, x, y, .. veri.DersIdleri]);
        return (kurucu, y, veri.DersIdleri);
    }
}
