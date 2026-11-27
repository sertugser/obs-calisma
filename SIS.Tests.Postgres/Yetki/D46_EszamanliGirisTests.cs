using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SIS.Infrastructure.Persistence;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// D46: aynı hesaba eşzamanlı giriş denemeleri (gerçek giriş ucu). Kullanıcı satırı bu istek yüklediğinden beri değiştiyse giriş
// 500 yerine bugünkü yanıtı verir ve Identity güncellemesi taze değerle yeniden denenir. Araya girme, uygulamanın context'ine eklenen
// komut kesicisiyle "AspNetUsers" güncellemesinden hemen önce ve belirlenimli kurulur; paralel testlerin iddiaları her sıralamada geçerlidir.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class D46_EszamanliGirisTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "d46-kullanici@ornek.test";
    private const string Hatali = "Invalid email or password.";
    private const string GecersizGerekce = "{\"reason\":\"InvalidPassword\"}";
    private const string KilitGerekce = "{\"reason\":\"AccountLocked\"}";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Araya giren güncelleme sayacı 1'e çıkarır; bu isteğin artışı kaybolmaz (sayaç 2) ve başarısız giriş kaydı yazılır.
    [Fact]
    public async Task D46_YanlisParola_ArayaGirenGuncelleme_401_ArtisKaybolmaz()
    {
        var (yanit, kesici) = await ArayaGirerekGirisAsync(dogruParola: false, enCok: 1);

        await ReddedilirAsync(yanit, Hatali);
        Assert.Equal(1, kesici.Calisma);
        Assert.Equal((2, false), await SayacVeKilitAsync());
        Assert.Equal([GecersizGerekce], await BasarisizKayitlarAsync());
    }

    // Her denemeye araya girilir: tam üç deneme, yine 401 ve kayıt; bu isteğin artışı kaybolur (bilinen sınır), sayaç yalnız araya girenlerin.
    [Fact]
    public async Task D46_YanlisParola_HerDenemedeArayaGirilir_UcDenemedenSonra401()
    {
        var (yanit, kesici) = await ArayaGirerekGirisAsync(dogruParola: false, enCok: 10);

        await ReddedilirAsync(yanit, Hatali);
        Assert.Equal(3, kesici.Calisma);
        Assert.Equal((3, false), await SayacVeKilitAsync());
        Assert.Equal([GecersizGerekce], await BasarisizKayitlarAsync());
    }

    // Doğru parola, sayaç 2; sıfırlamadan hemen önce araya giren güncelleme sayacı 3'e çıkarır: giriş kabul, sayaç sıfır, başarı kaydı.
    [Fact]
    public async Task D46_DogruParola_SifirlamadaArayaGirenGuncelleme_200_SayacSifirlanir()
    {
        var (yanit, kesici) = await ArayaGirerekGirisAsync(dogruParola: true, enCok: 1, sayac: 2);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal(1, kesici.Calisma);
        Assert.Equal((0, false), await SayacVeKilitAsync());
        await using var db = fixture.YeniContext();
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "LoginSuccess" && a.EntityId == Email));
    }

    // Üç eşzamanlı yanlış parola: bir istek en çok iki kez kaybeder, üç deneme yeter. Hepsi 401, sayaç 3, üç kayıt.
    [Fact]
    public async Task D46_UcEszamanliYanlisParola_Hepsi401_SayacUc()
    {
        var (kurucu, istemci) = await KurAsync();

        var yanitlar = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => kurucu.GirisAsync(istemci, Email, dogruParola: false)));

        foreach (var yanit in yanitlar)
            await ReddedilirAsync(yanit, Hatali);
        Assert.Equal((3, false), await SayacVeKilitAsync());
        Assert.Equal([GecersizGerekce, GecersizGerekce, GecersizGerekce], await BasarisizKayitlarAsync());
    }

    // Sayaç 4 iken üç eşzamanlı yanlış parola: ilk başarılı artış hesabı kilitler (sayaç sıfırlanır); sonraki artışlar kilidi taze görür.
    // Hepsi 401 ve kilit gerekçeli; hesap kilitli.
    [Fact]
    public async Task D46_EsikteUcEszamanliYanlisParola_HesapKilitlenir_HepsiKilitGerekceli()
    {
        var (kurucu, istemci) = await KurAsync(sayac: 4);

        var yanitlar = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => kurucu.GirisAsync(istemci, Email, dogruParola: false)));

        Assert.All(yanitlar, yanit => Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode));
        var (sayac, kilitli) = await SayacVeKilitAsync();
        Assert.True(kilitli);
        Assert.InRange(sayac, 0, 2);
        Assert.Equal([KilitGerekce, KilitGerekce, KilitGerekce], await BasarisizKayitlarAsync());
    }

    // Sekiz eşzamanlı yanlış parola (hız sınırı içinde): hepsi 401, 500 yok, sekiz başarısız giriş kaydı.
    [Fact]
    public async Task D46_SekizEszamanliYanlisParola_Hepsi401_SekizKayit()
    {
        var (kurucu, istemci) = await KurAsync();

        var yanitlar = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => kurucu.GirisAsync(istemci, Email, dogruParola: false)));

        Assert.All(yanitlar, yanit => Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode));
        Assert.Equal(8, (await BasarisizKayitlarAsync()).Length);
    }

    // Doğru parola ile eşzamanlı bir yanlış parola (sayaç 2): 500 yok; sıralamaya göre sayaç 0 ya da 1.
    [Fact]
    public async Task D46_DogruVeYanlisParolaEszamanli_500Yok_SayacSifirYaDaBir()
    {
        var (kurucu, istemci) = await KurAsync(sayac: 2);

        var yanitlar = await Task.WhenAll(kurucu.GirisAsync(istemci, Email), kurucu.GirisAsync(istemci, Email, dogruParola: false));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Unauthorized], yanitlar.Select(yanit => yanit.StatusCode));
        Assert.Contains((await SayacVeKilitAsync()).Sayac, new[] { 0, 1 });
    }

    private async Task<(YetkiKurucu, HttpClient)> KurAsync(int sayac = 0)
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.KullaniciAsync(Email);
        await SayacVerAsync(sayac);
        return (kurucu, uygulama.Al(fixture.BaglantiCumlesi).CreateClient());
    }

    // Kesici ayrı bir uygulama örneğinin context'ine eklenir (ayrı hız sınırı sayacı).
    private async Task<(HttpResponseMessage, KullaniciGuncellemeKesicisi)> ArayaGirerekGirisAsync(bool dogruParola, int enCok, int sayac = 0)
    {
        var kesici = new KullaniciGuncellemeKesicisi(fixture, Email, enCok);
        using var kesicili = uygulama.Al(fixture.BaglantiCumlesi).WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.ConfigureDbContext<AppDbContext>((_, o) => o.AddInterceptors(kesici))));
        var kurucu = new YetkiKurucu(fixture, kesicili);
        await kurucu.KullaniciAsync(Email);
        await SayacVerAsync(sayac);
        using var istemci = kesicili.CreateClient();
        return (await kurucu.GirisAsync(istemci, Email, dogruParola), kesici);
    }

    private async Task SayacVerAsync(int sayac)
    {
        await using var db = fixture.YeniContext();
        await db.Users.Where(u => u.Email == Email).ExecuteUpdateAsync(u => u.SetProperty(k => k.AccessFailedCount, sayac));
    }

    private async Task<(int Sayac, bool Kilitli)> SayacVeKilitAsync()
    {
        await using var db = fixture.YeniContext();
        var kullanici = await db.Users.AsNoTracking().SingleAsync(u => u.Email == Email);
        return (kullanici.AccessFailedCount, kullanici.LockoutEnd > DateTimeOffset.UtcNow);
    }

    private async Task<string[]> BasarisizKayitlarAsync()
    {
        await using var db = fixture.YeniContext();
        return await db.AuditLogs.AsNoTracking().Where(a => a.Action == "LoginFailed" && a.EntityId == Email)
            .OrderBy(a => a.Id).Select(a => a.NewValue ?? "").ToArrayAsync();
    }

    private static async Task ReddedilirAsync(HttpResponseMessage yanit, string hata)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal(hata, govde.RootElement.GetProperty("error").GetString());
    }
}

/// <summary>
/// Bağlı context'in "AspNetUsers" güncelleme komutundan hemen önce aynı kullanıcının sayacını ayrı context'le bir artırır ve
/// eşzamanlılık damgasını yeniler (başka bir isteğin başarısız girişi gibi); en çok <paramref name="enCok"/> kez.
/// </summary>
internal sealed class KullaniciGuncellemeKesicisi(PostgresFixture fixture, string email, int enCok) : DbCommandInterceptor
{
    public int Calisma { get; private set; }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand komut, CommandEventData olay, InterceptionResult<int> sonuc, CancellationToken ct = default)
    {
        await ArayaGirAsync(komut);
        return sonuc;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand komut, CommandEventData olay, InterceptionResult<DbDataReader> sonuc, CancellationToken ct = default)
    {
        await ArayaGirAsync(komut);
        return sonuc;
    }

    private async Task ArayaGirAsync(DbCommand komut)
    {
        if (Calisma >= enCok || !komut.CommandText.Contains("UPDATE \"AspNetUsers\""))
            return;
        Calisma++;
        var damga = Guid.NewGuid().ToString();
        await using var db = fixture.YeniContext();
        await db.Users.Where(u => u.Email == email).ExecuteUpdateAsync(u => u
            .SetProperty(k => k.AccessFailedCount, k => k.AccessFailedCount + 1)
            .SetProperty(k => k.ConcurrencyStamp, damga));
    }
}
