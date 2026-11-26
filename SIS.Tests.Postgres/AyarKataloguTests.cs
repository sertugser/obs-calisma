using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Postgres;

// Adım 3.4a: sistem ayarları, web modül ayarları ve kısıt tanımları katalog kuralıyla.
// Yöneticinin değeri korunur, yöneticinin sildiği geri gelmez, kataloğun açıklaması geri yazılır.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class AyarKataloguTests(PostgresFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.TemizleAsync();
        await EsitleAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task EsitleAsync()
    {
        await using var db = fixture.YeniContext();
        await SeedData.SeedSystemConfigAsync(db);
        await SeedData.SeedWebModuleSettingsAsync(db);
        await SeedData.SeedConstraintDefinitionsAsync(db);
    }

    [Fact]
    public async Task TazeKurulum_UcTurdeHerAnahtarYazilir_VeKayitta()
    {
        await using var db = fixture.YeniContext();
        Assert.Equal(115, await db.KatalogKayitlari.CountAsync(k => k.Tur == "sistem-ayari"));
        Assert.Equal(62, await db.KatalogKayitlari.CountAsync(k => k.Tur == "web-modul-ayari"));
        Assert.Equal(26, await db.KatalogKayitlari.CountAsync(k => k.Tur == "kisit"));
        Assert.Equal(26, await db.ConstraintDefinitions.CountAsync());
        Assert.Equal(62, await db.WebModuleSettings.CountAsync());
        Assert.Equal(1, await db.SystemConfigs.CountAsync(c => c.Key == "Classroom.AmphiThreshold"));
        Assert.Equal("Scheduling", (await db.SystemConfigs.SingleAsync(c => c.Key == "Classroom.AmphiThreshold")).Category);
    }

    [Fact]
    public async Task IkinciEsitleme_HicbirTablonunSatirSayisiniDegistirmez()
    {
        var once = await SatirSayilariAsync();
        await EsitleAsync();
        Assert.Equal(once, await SatirSayilariAsync());
    }

    [Fact]
    public async Task YoneticininDegeriKorunur_SildigiGeriGelmez_AciklamaGeriYazilir()
    {
        string degisenAyar, silinenAyar, aciklamasiBozulan, kisitKodu, webAnahtari;
        string aciklama;
        await using (var db = fixture.YeniContext())
        {
            var ayarlar = await db.SystemConfigs.OrderBy(c => c.Key).Take(3).ToListAsync();
            degisenAyar = ayarlar[0].Key;
            ayarlar[0].Value = "yonetici-degeri";
            silinenAyar = ayarlar[1].Key;
            db.SystemConfigs.Remove(ayarlar[1]);
            aciklamasiBozulan = ayarlar[2].Key;
            aciklama = ayarlar[2].Description!;
            ayarlar[2].Description = "elle değişti";

            var kisit = await db.ConstraintDefinitions.OrderBy(d => d.Code).FirstAsync();
            kisitKodu = kisit.Code;
            kisit.IsEnabled = !kisit.IsEnabled;
            kisit.Weight = 9;

            var web = await db.WebModuleSettings.OrderBy(s => s.Id).FirstAsync();
            webAnahtari = $"{web.ModuleKey}:{web.Key}";
            web.Value = "yonetici-degeri";
            await db.SaveChangesAsync();
        }
        bool kisitAcik;
        await using (var db = fixture.YeniContext())
            kisitAcik = (await db.ConstraintDefinitions.SingleAsync(d => d.Code == kisitKodu)).IsEnabled;

        await EsitleAsync();

        await using var kontrol = fixture.YeniContext();
        Assert.Equal("yonetici-degeri", (await kontrol.SystemConfigs.SingleAsync(c => c.Key == degisenAyar)).Value);
        Assert.False(await kontrol.SystemConfigs.AnyAsync(c => c.Key == silinenAyar));
        Assert.Equal(aciklama, (await kontrol.SystemConfigs.SingleAsync(c => c.Key == aciklamasiBozulan)).Description);
        var k = await kontrol.ConstraintDefinitions.SingleAsync(d => d.Code == kisitKodu);
        Assert.Equal((kisitAcik, 9), (k.IsEnabled, k.Weight));
        var w = (await kontrol.WebModuleSettings.ToListAsync()).Single(s => $"{s.ModuleKey}:{s.Key}" == webAnahtari);
        Assert.Equal("yonetici-degeri", w.Value);
    }

    private async Task<SortedDictionary<string, long>> SatirSayilariAsync()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        return new SortedDictionary<string, long>(await PostgresFixture.SatirSayilariAsync(baglanti));
    }
}
