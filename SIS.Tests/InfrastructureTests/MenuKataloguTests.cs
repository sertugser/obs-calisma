using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Persistence.Katalog;

namespace SIS.Tests.InfrastructureTests;

// Adım 3.3a: menü kataloğunun ağacı ve tutarlılığı.
public class MenuKataloguTests
{
    private static readonly DateTime Simdi = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    private static AppDbContext YeniContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Eski MenuStructureSeed ile alan alan eşdeğerlik 3.3a'da doğrulandı (İş paketi 23); eski seed
    // Adım 3.9b'de silindiği için burada yalnız ağacın biçimi sabitleniyor.
    [Fact]
    public async Task KatalogAgaciKurar_UstModulVeIzinBaglariyla()
    {
        await using var yeni = YeniContext();
        await SistemKatalogu.EsitleAsync(yeni, Simdi);
        await MenuKatalogu.EsitleAsync(yeni, Simdi);

        Assert.Equal(18, (await ModullerAsync(yeni)).Count);
        var ogeler = await OgelerAsync(yeni);
        Assert.Equal(74, ogeler.Count);
        Assert.All(ogeler, o => Assert.False(o.EndsWith('|'), o)); // her öğenin bir izni var
        Assert.Equal(2, await yeni.MenuModules.CountAsync(m => m.ParentId != null));
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
