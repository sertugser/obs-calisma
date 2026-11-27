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
    public async Task AltiTurdeDe_SilinenGeriGelmez_DegisenKorunur()
    {
        await using (var db = fixture.YeniContext())
        {
            db.CourseCategories.Remove(await db.CourseCategories.OrderBy(c => c.Code).FirstAsync());
            db.CurriculumCourseOpeningReasons.Remove(await db.CurriculumCourseOpeningReasons.OrderBy(r => r.Code).FirstAsync());
            db.ElectiveSubtypes.Remove(await db.ElectiveSubtypes.OrderBy(s => s.Code).FirstAsync());
            db.DocumentTypes.Remove(await db.DocumentTypes.OrderBy(d => d.Code).FirstAsync());
            db.ReportDefinitions.Remove(await db.ReportDefinitions.OrderBy(r => r.Code).FirstAsync());
            // Duyuru kategorisi silinemez (sistem); yönetici adını değiştirir.
            (await db.AnnouncementCategories.SingleAsync(c => c.Name == "Akademik")).Name = "Akademik Duyurular";
            (await db.CourseCategories.OrderBy(c => c.Code).Skip(1).FirstAsync()).Name = "Yöneticinin adı";
            (await db.CurriculumCourseOpeningReasons.OrderBy(r => r.Code).Skip(1).FirstAsync()).IsExcludedFromGpa ^= true;
            (await db.ElectiveSubtypes.OrderBy(s => s.Code).Skip(1).FirstAsync()).Name = "Yöneticinin adı";
            (await db.DocumentTypes.OrderBy(d => d.Code).Skip(1).FirstAsync()).Name = "Yöneticinin adı";
            (await db.ReportDefinitions.OrderBy(r => r.Code).Skip(1).FirstAsync()).Name = "Yöneticinin adı";
            await db.SaveChangesAsync();
        }
        var once = await DurumAsync();

        await EsitleAsync();

        Assert.Equal(once, await DurumAsync());
        await using var kontrol = fixture.YeniContext();
        Assert.False(await kontrol.AnnouncementCategories.AnyAsync(c => c.Name == "Akademik"));
    }

    [Fact]
    public async Task YazilanKayitlar_ListedekiAlanlarlaAyni()
    {
        await using var db = fixture.YeniContext();
        var nedenler = await db.CurriculumCourseOpeningReasons.ToDictionaryAsync(r => r.Code);
        foreach (var r in CurriculumCourseOpeningReasonSeed.All)
        {
            var v = nedenler[r.Code];
            Assert.Equal(
                (r.Name, r.Description, r.SortOrder, r.IsActive, r.AllowsAdvisorGrading, r.BypassScheduleConflict, r.BypassMandatoryAttendance, r.IsExcludedFromGpa, r.IsCreditFree, r.RequiresApproval, r.AllowsCustomGradingScale),
                (v.Name, v.Description, v.SortOrder, v.IsActive, v.AllowsAdvisorGrading, v.BypassScheduleConflict, v.BypassMandatoryAttendance, v.IsExcludedFromGpa, v.IsCreditFree, v.RequiresApproval, v.AllowsCustomGradingScale));
        }
        var turler = await db.ElectiveSubtypes.ToDictionaryAsync(s => s.Code);
        foreach (var s in ElectiveSubtypeSeed.Defaults)
            Assert.Equal((s.RequiresFaculty, s.ForbidsFaculty, s.RequiresDepartment, s.ForbidsDepartment), (turler[s.Code].RequiresFaculty, turler[s.Code].ForbidsFaculty, turler[s.Code].RequiresDepartment, turler[s.Code].ForbidsDepartment));
        Assert.Equal(6, await db.ReportDefinitions.CountAsync());
    }

    // Altı türün bütün satırları (anahtar ve ad), karşılaştırma için.
    private async Task<List<string>> DurumAsync()
    {
        await using var db = fixture.YeniContext();
        var satirlar = new List<string>();
        satirlar.AddRange((await db.CourseCategories.ToListAsync()).Select(c => $"kategori|{c.Code}|{c.Name}"));
        satirlar.AddRange((await db.CurriculumCourseOpeningReasons.ToListAsync()).Select(r => $"neden|{r.Code}|{r.Name}|{r.IsExcludedFromGpa}"));
        satirlar.AddRange((await db.ElectiveSubtypes.ToListAsync()).Select(s => $"secmeli|{s.Code}|{s.Name}"));
        satirlar.AddRange((await db.DocumentTypes.ToListAsync()).Select(d => $"belge|{d.Code}|{d.Name}"));
        satirlar.AddRange((await db.AnnouncementCategories.ToListAsync()).Select(c => $"duyuru|{c.Name}"));
        satirlar.AddRange((await db.ReportDefinitions.ToListAsync()).Select(r => $"rapor|{r.Code}|{r.Name}"));
        return satirlar.Order(StringComparer.Ordinal).ToList();
    }

    private async Task<SortedDictionary<string, long>> SatirSayilariAsync()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        return new SortedDictionary<string, long>(await PostgresFixture.SatirSayilariAsync(baglanti));
    }
}
