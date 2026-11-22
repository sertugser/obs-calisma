using System.Net;
using Microsoft.EntityFrameworkCore;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// A-04: giriş ucunun hız sınırı. Sabit pencere, ilk istekle başlar; testte tüm istekler aynı bölüme düşer (istemci IP'si yok).
// Sınıf kendi uygulama örneğini kullanır ve tek testtir: sayaç öteki sınıfların girişlerinden etkilenmez.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class A04_GirisHizSiniriTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "a04-olmayan@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // 41 istek art arda (en ucuz ret yolu: olmayan kullanıcı); karşı örnek: başka politika altındaki uç aynı anda 200.
    [Fact]
    public async Task A04_AyniKaynaktan41inciGirisDenemesi429Alir_BaskaPolitikadakiUc200()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        using var istemci = uygulama.Al(fixture.BaglantiCumlesi).CreateClient();
        var durumlar = new List<HttpStatusCode>();
        for (var i = 0; i < 41; i++)
            durumlar.Add((await kurucu.GirisAsync(istemci, Email, dogruParola: false)).StatusCode);
        var baskaPolitika = await istemci.GetAsync("/api/auth/login-settings");

        Assert.Equal(Enumerable.Repeat(HttpStatusCode.Unauthorized, 40).Append(HttpStatusCode.TooManyRequests), durumlar);
        Assert.Equal(HttpStatusCode.OK, baskaPolitika.StatusCode);
        // 41. istek uca ulaşmaz: başarısız giriş kaydı 40.
        await using var db = fixture.YeniContext();
        Assert.Equal(40, await db.AuditLogs.CountAsync(a => a.Action == "LoginFailed" && a.EntityId == Email));
    }
}
