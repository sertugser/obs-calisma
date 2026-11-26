using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence.Katalog;

namespace SIS.Tests.Postgres;

// Adım 3.2a: roller ve izinler sistem kataloğundan, gerçek PostgreSQL'de.
// Veritabanı yalnız migration uygulanmış hâldedir; migration'ın yazdığı izinler de durur.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class KatalogTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Simdi = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TazeKurulum_YediRolVeButunIzinler_HerAnahtarKayitta()
    {
        await using (var db = fixture.YeniContext())
        {
            // Migration'ın yazdığı izinler var ama kayıtta değil.
            Assert.True(await db.Permissions.AnyAsync());
            Assert.Equal(0, await db.KatalogKayitlari.CountAsync());
            await SistemKatalogu.EsitleAsync(db, Simdi);
        }

        await using var kontrol = fixture.YeniContext();
        Assert.Equal(SistemKatalogu.RolAdlari.Order(), (await kontrol.Roles.Select(r => r.Name!).ToListAsync()).Order());
        Assert.Equal(
            SistemKatalogu.Izinler.Kayitlar.Select(i => i.Key).Order(StringComparer.Ordinal),
            (await kontrol.Permissions.Select(p => p.Key).ToListAsync()).Order(StringComparer.Ordinal));
        Assert.Equal(
            SistemKatalogu.RolAdlari.Count + SistemKatalogu.Izinler.Kayitlar.Count,
            await kontrol.KatalogKayitlari.CountAsync());
        Assert.All(await kontrol.Roles.ToListAsync(), r => Assert.Equal(r.Name!.ToUpperInvariant(), r.NormalizedName));
    }

    [Fact]
    public async Task IkinciEsitleme_HicbirTablonunSatirSayisiniDegistirmez()
    {
        await using (var db = fixture.YeniContext())
            await SistemKatalogu.EsitleAsync(db, Simdi);
        var once = await SatirSayilariAsync();

        IReadOnlyList<KatalogSonucu> sonuc;
        await using (var db = fixture.YeniContext())
            sonuc = await SistemKatalogu.EsitleAsync(db, Simdi);

        Assert.Equal(once, await SatirSayilariAsync());
        Assert.All(sonuc, s => Assert.Equal(0, s.Eklenen + s.Guncellenen + s.Kaydedilen + s.Atlanan));
    }

    [Fact]
    public async Task SilinenIzin_GeriGelmez_DegisenAciklama_GeriYazilir()
    {
        var silinen = SistemKatalogu.Izinler.Kayitlar[0];
        var degisen = SistemKatalogu.Izinler.Kayitlar[1];
        await using (var db = fixture.YeniContext())
        {
            await SistemKatalogu.EsitleAsync(db, Simdi);
            db.Permissions.Remove(await db.Permissions.SingleAsync(p => p.Key == silinen.Key));
            (await db.Permissions.SingleAsync(p => p.Key == degisen.Key)).Description = "elle değişti";
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.YeniContext())
            await SistemKatalogu.EsitleAsync(db, Simdi);

        await using var kontrol = fixture.YeniContext();
        Assert.False(await kontrol.Permissions.AnyAsync(p => p.Key == silinen.Key));
        Assert.Equal(degisen.Description, (await kontrol.Permissions.SingleAsync(p => p.Key == degisen.Key)).Description);
    }

    // Denetim günlüğü her yazımda büyüdüğü için karşılaştırmanın dışında.
    private async Task<SortedDictionary<string, long>> SatirSayilariAsync()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        var sayilar = await PostgresFixture.SatirSayilariAsync(baglanti);
        sayilar.Remove("AuditLogs");
        return new SortedDictionary<string, long>(sayilar);
    }
}
