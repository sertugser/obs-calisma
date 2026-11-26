using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Persistence.Katalog;
using SIS.Infrastructure.Persistence.Seeds;

namespace SIS.Tests.InfrastructureTests;

// Adım 3.3a: menü kataloğu eski MenuStructureSeed ile aynı ağacı kurar.
public class MenuKataloguTests
{
    private static readonly DateTime Simdi = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    private static AppDbContext YeniContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task EskiSeedIleAyniAgaciKurar()
    {
        await using var eski = YeniContext();
        await SistemKatalogu.EsitleAsync(eski, Simdi);
        await MenuStructureSeed.SeedAsync(eski);

        await using var yeni = YeniContext();
        await SistemKatalogu.EsitleAsync(yeni, Simdi);
        await MenuKatalogu.EsitleAsync(yeni, Simdi);

        Assert.Equal(await ModullerAsync(eski), await ModullerAsync(yeni));
        Assert.Equal(await OgelerAsync(eski), await OgelerAsync(yeni));
        Assert.Equal(18, (await ModullerAsync(yeni)).Count);
        Assert.Equal(74, (await OgelerAsync(yeni)).Count);
    }

    [Fact]
    public void HerOgeninIzniKatalogda_SluglarTekil()
    {
        var izinler = SistemKatalogu.Izinler.Kayitlar.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        Assert.All(MenuKatalogu.Ogeler, o => Assert.Contains(o.IzinAnahtari, izinler));
        KatalogEsitleyici.AnahtarlariDenetle("menu-modulu", MenuKatalogu.Moduller.Select(m => m.Slug));
        KatalogEsitleyici.AnahtarlariDenetle("menu-ogesi", MenuKatalogu.Ogeler.Select(o => o.Slug));
        var moduller = MenuKatalogu.Moduller.Select(m => m.Slug).ToHashSet(StringComparer.Ordinal);
        Assert.All(MenuKatalogu.Ogeler, o => Assert.Contains(o.ModulSlug, moduller));
    }

    [Fact]
    public async Task YoneticiAyniSluglaOgeEkleyemez()
    {
        await using var db = YeniContext();
        await SistemKatalogu.EsitleAsync(db, Simdi);
        await MenuKatalogu.EsitleAsync(db, Simdi);
        var oge = MenuKatalogu.Ogeler[0];
        var baskaModul = await db.MenuModules.FirstAsync(m => m.Slug != oge.ModulSlug && m.ParentId == null);

        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => new SIS.Infrastructure.Services.MenuAdminService(db)
            .CreateItemAsync(new SIS.Application.Identity.DTOs.CreateMenuItemDto
            {
                ModuleId = baskaModul.Id,
                Label = "Kopya",
                Route = "/kopya-sayfa",
                Slug = oge.Slug,
                PermissionKeys = [oge.IzinAnahtari],
            }));
        Assert.Contains(oge.Slug, hata.Message);
    }

    private static async Task<List<string>> ModullerAsync(AppDbContext db)
    {
        var moduller = await db.MenuModules.ToListAsync();
        var slug = moduller.ToDictionary(m => m.Id, m => m.Slug);
        return moduller
            .Select(m => $"{m.Slug}|{(m.ParentId is int p ? slug[p] : "")}|{m.Label}|{m.Icon}|{m.SortOrder}|{m.AllowedRolesCsv}|{m.IsActive}")
            .Order(StringComparer.Ordinal).ToList();
    }

    private static async Task<List<string>> OgelerAsync(AppDbContext db)
    {
        var modul = await db.MenuModules.ToDictionaryAsync(m => m.Id, m => m.Slug);
        var izin = await db.Permissions.ToDictionaryAsync(p => p.Id, p => p.Key);
        var ogeler = await db.MenuItems.Include(i => i.MenuItemPermissions).ToListAsync();
        return ogeler
            .Select(i => $"{modul[i.ModuleId]}|{i.Slug}|{i.Label}|{i.Route}|{i.SortOrder}|{i.OpenInNewTab}|{i.IsActive}|" +
                         string.Join(",", i.MenuItemPermissions.Select(mp => izin[mp.PermissionId]).Order(StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal).ToList();
    }
}
