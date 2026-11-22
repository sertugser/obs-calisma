using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// A-03: parola değişim tarihi eski olan kullanıcı (gerçek giriş, gerçek ara katman). Karşı örnekte tek fark tarih alanının boş olması.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class A03_ParolaYenilemeTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "a03-kullanici@ornek.test";
    private const string Izin = "menu.akademik-donem.view";
    private static readonly DateTime EskiTarih = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A03_ParolaTarihiEskiKullanici_GiristeDegisiklikZorunlu_IzinliYolDisinda403VeKodAlir()
    {
        var (_, istemci, zorunlu) = await KurAsync(EskiTarih);

        var yanit = await istemci.GetAsync("/api/admin/terms");
        var izinli = await istemci.GetAsync("/api/me/permissions");

        Assert.True(zorunlu);
        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal("PasswordChangeRequired", govde.RootElement.GetProperty("code").GetString());
        Assert.Equal("Sifre degisikligi zorunlu. Devam etmek icin sifrenizi guncelleyin.", govde.RootElement.GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.OK, izinli.StatusCode);
        Assert.Equal([Izin], await izinli.Content.ReadFromJsonAsync<string[]>() ?? []);
    }

    [Fact]
    public async Task A03_ParolaDegistirildiktenSonra_YeniJetonlaAyniIstek200Alir()
    {
        var (kurucu, istemci, zorunlu) = await KurAsync(EskiTarih);

        var degisim = await kurucu.ParolaDegistirAsync(istemci, Email);

        Assert.True(zorunlu);
        Assert.Equal(HttpStatusCode.OK, degisim.StatusCode);
        using (var govde = JsonDocument.Parse(await degisim.Content.ReadAsStringAsync()))
        {
            Assert.False(govde.RootElement.GetProperty("passwordChangeRequired").GetBoolean());
            istemci.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", govde.RootElement.GetProperty("token").GetString());
        }

        await DonemlerDonerAsync(await istemci.GetAsync("/api/admin/terms"));
        await using var db = fixture.YeniContext();
        var tarih = await db.Users.AsNoTracking().Where(u => u.Email == Email).Select(u => u.PasswordChangedAtUtc).SingleAsync();
        Assert.NotNull(tarih);
        Assert.NotEqual<DateTime?>(EskiTarih, tarih);
    }

    // Karşı örnek: aynı rol, izin ve istek; parola değişim tarihi boş.
    [Fact]
    public async Task A03_ParolaTarihiBosKullanici_AyniIstek200Alir()
    {
        var (_, istemci, zorunlu) = await KurAsync(null);

        var yanit = await istemci.GetAsync("/api/admin/terms");

        Assert.False(zorunlu);
        await DonemlerDonerAsync(yanit);
    }

    private async Task<(YetkiKurucu, HttpClient, bool Zorunlu)> KurAsync(DateTime? parolaTarihi)
    {
        var app = uygulama.Al(fixture.BaglantiCumlesi);
        var kurucu = new YetkiKurucu(fixture, app);
        await kurucu.RolAsync("Student", Izin);
        await kurucu.KullaniciAsync(Email, "Student");
        await using (var db = fixture.YeniContext())
        {
            await db.Users.Where(u => u.Email == Email)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.PasswordChangedAtUtc, parolaTarihi));
        }

        var istemci = app.CreateClient();
        var giris = await kurucu.GirisAsync(istemci, Email);
        Assert.Equal(HttpStatusCode.OK, giris.StatusCode);
        using var govde = JsonDocument.Parse(await giris.Content.ReadAsStringAsync());
        istemci.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", govde.RootElement.GetProperty("token").GetString());
        return (kurucu, istemci, govde.RootElement.GetProperty("passwordChangeRequired").GetBoolean());
    }

    // 200 ve dönen dönem kimlikleri veritabanındakilerle aynı (migration verisindeki dönemler).
    private async Task DonemlerDonerAsync(HttpResponseMessage yanit)
    {
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var donen = govde.RootElement.EnumerateArray().Select(t => t.GetProperty("id").GetInt32()).Order().ToList();
        await using var db = fixture.YeniContext();
        var beklenen = await db.AcademicTerms.AsNoTracking().OrderBy(t => t.Id).Select(t => t.Id).ToListAsync();
        Assert.NotEmpty(beklenen);
        Assert.Equal(beklenen, donen);
    }
}
