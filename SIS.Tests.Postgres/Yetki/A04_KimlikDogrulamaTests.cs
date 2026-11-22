using System.Net;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// A-04: jetonsuz istek 401 (Bearer meydan okumasıyla); izni olmayan rolün jetonu 403.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class A04_KimlikDogrulamaTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "a04-kullanici@ornek.test";
    private const string Donemler = "/api/admin/terms";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // 1. satır: sınıfında [Authorize] olan uç. 2. satır: sınıfında [Authorize] olmayan uç (yalnız genel yetki politikası).
    // Karşı örnek aynı testte: aynı uç, izni olan rolün jetonuyla 200.
    [Theory]
    [InlineData(Donemler, "menu.akademik-donem.view")]
    [InlineData("/api/admin/course-lookups/opening-reasons", "menu.mufredat-islemleri.view")]
    public async Task A04_JetonsuzIstek401VeBearerMeydanOkumasiAlir_IzinliJetonla200(string yol, string izin)
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("Student", izin);
        await kurucu.KullaniciAsync(Email, "Student");
        using var jetonsuz = uygulama.Al(fixture.BaglantiCumlesi).CreateClient();

        var yanit = await jetonsuz.GetAsync(yol);

        Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
        Assert.Equal("Bearer", Assert.Single(yanit.Headers.WwwAuthenticate).Scheme);
        using var izinli = await kurucu.IstemciAsync(Email);
        Assert.Equal(HttpStatusCode.OK, (await izinli.GetAsync(yol)).StatusCode);
    }

    // Aynı uç, rolünde izin satırı olmayan kullanıcının jetonuyla 403 alır; meydan okuma başlığı yoktur.
    [Fact]
    public async Task A04_IzinsizJetonla403Alir_MeydanOkumaBasligiYok()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("Student");
        await kurucu.KullaniciAsync(Email, "Student");
        using var izinsiz = await kurucu.IstemciAsync(Email);

        var yanit = await izinsiz.GetAsync(Donemler);

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        Assert.Empty(yanit.Headers.WwwAuthenticate);
    }
}
