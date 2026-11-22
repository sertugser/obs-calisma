using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// A-02: art arda yanlış parola denemelerinde hesap kilidi (gerçek giriş ucu, Identity kilit alanları).
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class A02_HesapKilidiTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "a02-kullanici@ornek.test";
    private const string Hatali = "Invalid email or password.";
    private const string GecersizGerekce = "{\"reason\":\"InvalidPassword\"}";
    private const string KilitGerekce = "{\"reason\":\"AccountLocked\"}";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A02_BesYanlisParola_HesapKilitlenir_DogruParolaDaReddedilir()
    {
        var (kurucu, istemci) = await KurAsync();
        for (var i = 0; i < 4; i++)
            await ReddedilirAsync(await kurucu.GirisAsync(istemci, Email, dogruParola: false), Hatali);
        await ReddedilirAsync(await kurucu.GirisAsync(istemci, Email, dogruParola: false),
            "Account is locked due to multiple failed login attempts. Please try again later.");
        await ReddedilirAsync(await kurucu.GirisAsync(istemci, Email), "Account is locked. Please try again later.");

        await using var db = fixture.YeniContext();
        var kullanici = await db.Users.AsNoTracking().SingleAsync(u => u.Email == Email);
        var kayitlar = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "LoginFailed" && a.EntityId == Email).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal([GecersizGerekce, GecersizGerekce, GecersizGerekce, GecersizGerekce, KilitGerekce, KilitGerekce],
            kayitlar.Select(k => k.NewValue));
        // Kilit bitişi ile kilidi başlatan (5.) başarısız girişin kaydı arasındaki fark; iki zaman da uygulamadan gelir.
        Assert.NotNull(kullanici.LockoutEnd);
        var fark = kullanici.LockoutEnd.Value.UtcDateTime - kayitlar[4].Timestamp;
        Assert.InRange(fark, TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(30));
    }

    // Karşı örnek: eşiğin bir altında (4 yanlış) doğru parola kabul edilir, sayaç sıfırlanır, kilit yoktur.
    [Fact]
    public async Task A02_DortYanlisParola_DogruParolaKabulEdilir_SayacSifirlanir()
    {
        var (kurucu, istemci) = await KurAsync();
        for (var i = 0; i < 4; i++)
            await ReddedilirAsync(await kurucu.GirisAsync(istemci, Email, dogruParola: false), Hatali);

        var giris = await kurucu.GirisAsync(istemci, Email);

        Assert.Equal(HttpStatusCode.OK, giris.StatusCode);
        using var govde = JsonDocument.Parse(await giris.Content.ReadAsStringAsync());
        Assert.True(govde.RootElement.GetProperty("success").GetBoolean());
        await using var db = fixture.YeniContext();
        var kullanici = await db.Users.AsNoTracking().SingleAsync(u => u.Email == Email);
        Assert.Equal(0, kullanici.AccessFailedCount);
        Assert.Null(kullanici.LockoutEnd);
    }

    private async Task<(YetkiKurucu, HttpClient)> KurAsync()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.KullaniciAsync(Email);
        return (kurucu, uygulama.Al(fixture.BaglantiCumlesi).CreateClient());
    }

    private static async Task ReddedilirAsync(HttpResponseMessage yanit, string hata)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.False(govde.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(hata, govde.RootElement.GetProperty("error").GetString());
    }
}
