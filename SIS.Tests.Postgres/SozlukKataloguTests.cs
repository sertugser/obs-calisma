using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Persistence.Seeds;

namespace SIS.Tests.Postgres;

// Adım 3.5b: sözlük verisi (ders kategorisi, açma nedeni, seçmeli alt türü, belge tipi, duyuru
// kategorisi, rapor) katalog kuralıyla: eksik eklenir, silinen geri gelmez, var olana dokunulmaz.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class SozlukKataloguTests(PostgresFixture fixture) : IAsyncLifetime
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
        await CurriculumCourseOpeningReasonSeed.SeedAsync(db);
        await CourseCategorySeed.SeedAsync(db);
        await ElectiveSubtypeSeed.SeedAsync(db);
        await DocumentTypeSeed.SeedAsync(db);
        await SeedData.SeedAnnouncementCategoriesAsync(db);
        await ReportSeed.SeedAsync(db);
    }

    [Fact]
    public async Task TazeKurulum_HerTurYazilir_IkinciEsitlemeDegistirmez()
    {
        await using (var db = fixture.YeniContext())
        {
            Assert.Equal(CourseCategorySeed.All.Count, await db.CourseCategories.CountAsync());
            Assert.Equal(CurriculumCourseOpeningReasonSeed.All.Count, await db.CurriculumCourseOpeningReasons.CountAsync());
            Assert.Equal(DocumentTypeSeed.Defaults.Count(), await db.DocumentTypes.CountAsync());
            Assert.Equal(4, await db.AnnouncementCategories.CountAsync());
            Assert.True(await db.ReportDefinitions.AnyAsync());
            // Migration seçmeli alt türlerini yazmıştı; katalog yalnız kayda alır, yinelemez.
            Assert.Equal(ElectiveSubtypeSeed.Defaults.Count(), await db.ElectiveSubtypes.CountAsync());
        }

        var once = await SatirSayilariAsync();
        await EsitleAsync();
        Assert.Equal(once, await SatirSayilariAsync());
    }

    [Fact]
    public async Task SilinenGeriGelmez_DegisenKorunur()
    {
        string silinenKategori, degisenBelge;
        await using (var db = fixture.YeniContext())
        {
            var kategori = await db.CourseCategories.OrderBy(c => c.Code).FirstAsync();
            silinenKategori = kategori.Code;
            db.CourseCategories.Remove(kategori);
            var belge = await db.DocumentTypes.OrderBy(d => d.Code).FirstAsync();
            degisenBelge = belge.Code;
            belge.Name = "Yöneticinin adı";
            await db.SaveChangesAsync();
        }

        await EsitleAsync();

        await using var kontrol = fixture.YeniContext();
        Assert.False(await kontrol.CourseCategories.AnyAsync(c => c.Code == silinenKategori));
        Assert.Equal("Yöneticinin adı", (await kontrol.DocumentTypes.SingleAsync(d => d.Code == degisenBelge)).Name);
    }

    private async Task<SortedDictionary<string, long>> SatirSayilariAsync()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        return new SortedDictionary<string, long>(await PostgresFixture.SatirSayilariAsync(baglanti));
    }
}
