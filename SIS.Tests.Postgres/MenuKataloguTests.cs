using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence.Katalog;

namespace SIS.Tests.Postgres;

// Adım 3.3a: menü kataloğu gerçek PostgreSQL'de; yöneticinin değişikliği ve silmesi korunur.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class MenuKataloguTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Simdi = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await fixture.TemizleAsync();
        await using var db = fixture.YeniContext();
        await SistemKatalogu.EsitleAsync(db, Simdi);
        await MenuKatalogu.EsitleAsync(db, Simdi);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TazeKurulum_ButunModullerVeOgeler_HerOgeninBirIzni()
    {
        await using var db = fixture.YeniContext();
        Assert.Equal(MenuKatalogu.Moduller.Count, await db.MenuModules.CountAsync());
        Assert.Equal(MenuKatalogu.Ogeler.Count, await db.MenuItems.CountAsync());
        Assert.Equal(MenuKatalogu.Ogeler.Count, await db.MenuItemPermissions.CountAsync());
        Assert.Equal(2, await db.MenuModules.CountAsync(m => m.ParentId != null));
    }

    [Fact]
    public async Task IkinciEsitleme_HicbirTablonunSatirSayisiniDegistirmez()
    {
        var once = await SatirSayilariAsync();
        await using (var db = fixture.YeniContext())
            await MenuKatalogu.EsitleAsync(db, Simdi);
        Assert.Equal(once, await SatirSayilariAsync());
    }

    [Fact]
    public async Task YoneticininSildigiVeDegistirdigi_Korunur()
    {
        var silinenOge = MenuKatalogu.Ogeler[0];
        var degisenOge = MenuKatalogu.Ogeler[1];
        // Öteki iki öğeyi içermeyen, öğesi olan bir üst modül.
        var silinenModul = MenuKatalogu.Moduller.First(m => m.UstSlug is null
            && m.Slug != silinenOge.ModulSlug && m.Slug != degisenOge.ModulSlug
            && MenuKatalogu.Ogeler.Any(o => o.ModulSlug == m.Slug));
        await using (var db = fixture.YeniContext())
        {
            db.MenuItems.Remove(await db.MenuItems.SingleAsync(i => i.Slug == silinenOge.Slug));
            (await db.MenuItems.SingleAsync(i => i.Slug == degisenOge.Slug)).Label = "Yöneticinin adı";
            var modul = await db.MenuModules.Include(m => m.Items).SingleAsync(m => m.Slug == silinenModul.Slug);
            db.MenuItems.RemoveRange(modul.Items);
            db.MenuModules.Remove(modul);
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.YeniContext())
            await MenuKatalogu.EsitleAsync(db, Simdi);

        await using var kontrol = fixture.YeniContext();
        Assert.False(await kontrol.MenuItems.AnyAsync(i => i.Slug == silinenOge.Slug));
        Assert.Equal("Yöneticinin adı", (await kontrol.MenuItems.SingleAsync(i => i.Slug == degisenOge.Slug)).Label);
        Assert.False(await kontrol.MenuModules.AnyAsync(m => m.Slug == silinenModul.Slug));
        foreach (var oge in MenuKatalogu.Ogeler.Where(o => o.ModulSlug == silinenModul.Slug))
            Assert.False(await kontrol.MenuItems.AnyAsync(i => i.Slug == oge.Slug), oge.Slug);
    }

    private async Task<SortedDictionary<string, long>> SatirSayilariAsync()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        return new SortedDictionary<string, long>(await PostgresFixture.SatirSayilariAsync(baglanti));
    }
}
