using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.CalendarGenerator;

namespace SIS.Tests.InfrastructureTests;

// Adım 3.5a: takvim şablonu ve tatil varsayılanları okurken yöneticinin verisini ezmez.
public class TakvimVarsayilanlariTests
{
    private static AppDbContext YeniContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task BosTablodaOkuma_VarsayilanlariYazar()
    {
        await using var db = YeniContext();
        var servis = new CalendarTemplateService(db);

        Assert.NotEmpty(await servis.GetTemplatesAsync());
        Assert.NotEmpty(await servis.GetHolidayDefinitionsAsync());
    }

    [Fact]
    public async Task Okuma_YoneticininDuzenledigiSablonVeTatiliEzmez()
    {
        await using var db = YeniContext();
        var servis = new CalendarTemplateService(db);
        await servis.EnsureDefaultsAsync();
        var sablon = await db.CalendarTemplates.FirstAsync();
        sablon.ItemsJson = "[]";
        sablon.Name = "Yöneticinin şablonu";
        var tatil = await db.HolidayDefinitions.FirstAsync();
        tatil.Name = "Yöneticinin tatili";
        await db.SaveChangesAsync();

        await servis.GetTemplatesAsync();
        await servis.GetHolidayDefinitionsAsync();

        await db.Entry(sablon).ReloadAsync();
        await db.Entry(tatil).ReloadAsync();
        Assert.Equal(("Yöneticinin şablonu", "[]"), (sablon.Name, sablon.ItemsJson));
        Assert.Equal("Yöneticinin tatili", tatil.Name);
    }
}
