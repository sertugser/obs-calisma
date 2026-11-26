using Microsoft.EntityFrameworkCore;
using SIS.Domain.Enums;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Persistence.Katalog;
using SIS.Infrastructure.Persistence.Seeds;

namespace SIS.Tests.InfrastructureTests;

// Adım 3.2a: katalog eşitleme kuralı (ekle, yalnız kayda al, kataloğun alanını güncelle, silineni geri getirme).
public class KatalogEsitleyiciTests
{
    private static readonly DateTime Simdi = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    private static AppDbContext YeniContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static KatalogTuru<Permission, (string Key, PermissionCategory Category, string Description)> Tur(
        params (string Key, PermissionCategory Category, string Description)[] kayitlar) => new()
    {
        Ad = SistemKatalogu.Izinler.Ad,
        Kayitlar = kayitlar,
        KayitAnahtari = SistemKatalogu.Izinler.KayitAnahtari,
        VarlikAnahtari = SistemKatalogu.Izinler.VarlikAnahtari,
        Olustur = SistemKatalogu.Izinler.Olustur,
        KatalogAlanlariniYaz = SistemKatalogu.Izinler.KatalogAlanlariniYaz,
    };

    [Fact]
    public async Task BosVeritabaninda_HerKaydiEkler_VeAnahtariKaydeder()
    {
        await using var db = YeniContext();

        var sonuc = await KatalogEsitleyici.EsitleAsync(db, Tur(("a.view", PermissionCategory.Menu, "A")), Simdi);

        Assert.Equal(new KatalogSonucu("izin", 1, 0, 0, 0), sonuc);
        Assert.Equal("A", (await db.Permissions.SingleAsync()).Description);
        Assert.Equal("a.view", (await db.KatalogKayitlari.SingleAsync(k => k.Tur == "izin")).Anahtar);
    }

    [Fact]
    public async Task VeritabanindaOlupKayittaOlmayan_YalnizKaydaAlinir()
    {
        await using var db = YeniContext();
        db.Permissions.Add(new Permission { Key = "a.view", Category = PermissionCategory.Menu, Description = "A" });
        await db.SaveChangesAsync();

        var sonuc = await KatalogEsitleyici.EsitleAsync(db, Tur(("a.view", PermissionCategory.Menu, "A")), Simdi);

        Assert.Equal(new KatalogSonucu("izin", 0, 0, 1, 0), sonuc);
        Assert.Equal(1, await db.Permissions.CountAsync());
        var kayit = await db.KatalogKayitlari.SingleAsync();
        Assert.Equal(("izin", "a.view", Simdi), (kayit.Tur, kayit.Anahtar, kayit.IlkYazilma));
    }

    [Fact]
    public async Task KategoriDegismisse_GeriYazilir_BaskaTurunKaydiSayilmaz()
    {
        await using var db = YeniContext();
        db.Permissions.Add(new Permission { Key = "a.view", Category = PermissionCategory.Menu, Description = "A" });
        db.KatalogKayitlari.Add(new KatalogKaydi { Tur = "rol", Anahtar = "a.view", IlkYazilma = Simdi });
        await db.SaveChangesAsync();

        var sonuc = await KatalogEsitleyici.EsitleAsync(db, Tur(("a.view", PermissionCategory.System, "A")), Simdi);

        Assert.Equal(new KatalogSonucu("izin", 0, 1, 1, 0), sonuc);
        Assert.Equal(PermissionCategory.System, (await db.Permissions.SingleAsync()).Category);
    }

    [Fact]
    public async Task AnahtarBuyukKucukHarfeDuyarlidir()
    {
        await using var db = YeniContext();
        db.Permissions.Add(new Permission { Key = "A.VIEW", Category = PermissionCategory.Menu, Description = "A" });
        await db.SaveChangesAsync();

        var sonuc = await KatalogEsitleyici.EsitleAsync(db, Tur(("a.view", PermissionCategory.Menu, "A")), Simdi);

        Assert.Equal(1, sonuc.Eklenen);
        Assert.Equal(2, await db.Permissions.CountAsync());
    }

    [Fact]
    public async Task KatalogunAlaniDegismisse_GeriYazilir()
    {
        await using var db = YeniContext();
        await KatalogEsitleyici.EsitleAsync(db, Tur(("a.view", PermissionCategory.Menu, "A")), Simdi);
        (await db.Permissions.SingleAsync()).Description = "elle değişti";
        await db.SaveChangesAsync();

        var sonuc = await KatalogEsitleyici.EsitleAsync(db, Tur(("a.view", PermissionCategory.Menu, "A")), Simdi);

        Assert.Equal(new KatalogSonucu("izin", 0, 1, 0, 0), sonuc);
        Assert.Equal("A", (await db.Permissions.SingleAsync()).Description);
    }

    [Fact]
    public async Task KayittaOlupVeritabanindaOlmayan_GeriGetirilmez()
    {
        await using var db = YeniContext();
        await KatalogEsitleyici.EsitleAsync(db, Tur(("a.view", PermissionCategory.Menu, "A")), Simdi);
        db.Permissions.Remove(await db.Permissions.SingleAsync());
        await db.SaveChangesAsync();

        var sonuc = await KatalogEsitleyici.EsitleAsync(db, Tur(("a.view", PermissionCategory.Menu, "A")), Simdi);

        Assert.Equal(new KatalogSonucu("izin", 0, 0, 0, 1), sonuc);
        Assert.Equal(0, await db.Permissions.CountAsync());
    }

    [Fact]
    public async Task TekrarEdenAnahtar_HicbirSeyYazmadanReddedilir()
    {
        await using var db = YeniContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => KatalogEsitleyici.EsitleAsync(db,
            Tur(("a.view", PermissionCategory.Menu, "A"), ("a.view", PermissionCategory.Menu, "B")), Simdi));
        Assert.Equal(0, await db.Permissions.CountAsync());
    }

    [Fact]
    public void SistemKatalogu_TekrarEdenAnahtarIcermez_VeEskiListeyleAyni()
    {
        KatalogEsitleyici.AnahtarlariDenetle("rol", SistemKatalogu.RolAdlari);
        KatalogEsitleyici.AnahtarlariDenetle("izin", SistemKatalogu.Izinler.Kayitlar.Select(i => i.Key));
        Assert.Equal(7, SistemKatalogu.RolAdlari.Count);
        Assert.Equal(255, SistemKatalogu.Izinler.Kayitlar.Count);
        Assert.Same(PermissionSeed.All, SistemKatalogu.Izinler.Kayitlar);
    }
}
