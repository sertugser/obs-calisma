using Microsoft.EntityFrameworkCore;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Persistence.Katalog;

namespace SIS.Tests.InfrastructureTests;

// Adım 3.3b: rol-izin varsayılanları katalogdan; yöneticinin kaldırdığı atama geri gelmez.
public class RolIzinKataloguTests
{
    private static readonly DateTime Simdi = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    private static async Task<AppDbContext> KurulmusContextAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await SistemKatalogu.EsitleAsync(db, Simdi);
        return db;
    }

    private static async Task<List<string>> AtamalarAsync(AppDbContext db)
    {
        var rol = await db.Roles.ToDictionaryAsync(r => r.Id, r => r.Name!);
        var izin = await db.Permissions.ToDictionaryAsync(p => p.Id, p => p.Key);
        return (await db.RolePermissions.ToListAsync())
            .Select(rp => RolIzinKatalogu.Anahtar(rol[rp.RoleId], izin[rp.PermissionId]))
            .Order(StringComparer.Ordinal).ToList();
    }

    [Fact]
    public async Task TazeKurulum_EskiSeedinButunAtamalariniYazar()
    {
        await using var db = await KurulmusContextAsync();

        var sonuc = await RolIzinKatalogu.EsitleAsync(db, Simdi);

        Assert.Equal(432, sonuc.Eklenen);
        Assert.Equal(
            RolIzinKatalogu.Kayitlar.Select(k => RolIzinKatalogu.Anahtar(k.Rol, k.Izin)).Order(StringComparer.Ordinal),
            await AtamalarAsync(db));
        Assert.All(await db.RolePermissions.ToListAsync(), rp => Assert.Null(rp.SourceBundleId));
        Assert.DoesNotContain(RolIzinKatalogu.Kayitlar, k => k.Rol == "SystemAdministrator");
    }

    [Fact]
    public async Task KaldirilanAtama_GeriGelmez()
    {
        await using var db = await KurulmusContextAsync();
        await RolIzinKatalogu.EsitleAsync(db, Simdi);
        db.RolePermissions.Remove(await db.RolePermissions.FirstAsync());
        await db.SaveChangesAsync();

        var sonuc = await RolIzinKatalogu.EsitleAsync(db, Simdi);

        Assert.Equal(new KatalogSonucu("rol-izin", 0, 0, 0, 1), sonuc);
        Assert.Equal(431, await db.RolePermissions.CountAsync());
    }

    [Fact]
    public async Task KatalogdanOnceKurulmusSistem_IlkEsitlemedeHicbirAtamaEklenmez()
    {
        await using var db = await KurulmusContextAsync();
        // Eski kurulum: tablo dolu, katalog kaydı yok; yönetici atamaların çoğunu kaldırmış.
        var ogrenci = await db.Roles.SingleAsync(r => r.Name == "Student");
        var izin = await db.Permissions.FirstAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = ogrenci.Id, PermissionId = izin.Id, GrantedAt = Simdi });
        await db.SaveChangesAsync();

        var sonuc = await RolIzinKatalogu.EsitleAsync(db, Simdi);

        Assert.Equal(0, sonuc.Eklenen);
        Assert.Equal(1, await db.RolePermissions.CountAsync());
        Assert.Equal(RolIzinKatalogu.Kayitlar.Count, await db.KatalogKayitlari.CountAsync(k => k.Tur == "rol-izin"));
        // Sonraki eşitlemede de eklenmez.
        Assert.Equal(0, (await RolIzinKatalogu.EsitleAsync(db, Simdi)).Eklenen);
    }

    [Fact]
    public async Task PaketKaynakliAtamaVarsa_IkinciSatirYazilmaz()
    {
        await using var db = await KurulmusContextAsync();
        var ilk = RolIzinKatalogu.Kayitlar[0];
        var rol = await db.Roles.SingleAsync(r => r.Name == ilk.Rol);
        var izin = await db.Permissions.SingleAsync(p => p.Key == ilk.Izin);
        var paket = new PermissionBundle { Name = "Deneme" };
        db.PermissionBundles.Add(paket);
        await db.SaveChangesAsync();
        db.RolePermissions.Add(new RolePermission { RoleId = rol.Id, PermissionId = izin.Id, SourceBundleId = paket.Id, GrantedAt = Simdi });
        await db.SaveChangesAsync();
        // Tablo dolu sayılmasın diye tür önceden kayda alınmış gibi davranılır.
        db.KatalogKayitlari.Add(new KatalogKaydi { Tur = "rol-izin", Anahtar = "baska|anahtar", IlkYazilma = Simdi });
        await db.SaveChangesAsync();

        await RolIzinKatalogu.EsitleAsync(db, Simdi);

        Assert.Equal(1, await db.RolePermissions.CountAsync(rp => rp.RoleId == rol.Id && rp.PermissionId == izin.Id));
    }

    [Theory]
    [InlineData(true)]   // aynı çift hem elle hem paketten
    [InlineData(false)]  // aynı çift iki ayrı paketten
    public async Task AyniCiftinBirdenCokSatiriVarsa_EsitlemeCokmez_YeniSatirYazilmaz(bool elleDeVar)
    {
        await using var db = await KurulmusContextAsync();
        await RolIzinKatalogu.EsitleAsync(db, Simdi);
        var ilk = RolIzinKatalogu.Kayitlar[0];
        var rol = await db.Roles.SingleAsync(r => r.Name == ilk.Rol);
        var izin = await db.Permissions.SingleAsync(p => p.Key == ilk.Izin);
        if (!elleDeVar)
            db.RolePermissions.RemoveRange(db.RolePermissions.Where(rp => rp.RoleId == rol.Id && rp.PermissionId == izin.Id));
        var paketler = new[] { new PermissionBundle { Name = "P1" }, new PermissionBundle { Name = "P2" } };
        db.PermissionBundles.AddRange(paketler);
        await db.SaveChangesAsync();
        foreach (var paket in elleDeVar ? paketler[..1] : paketler)
            db.RolePermissions.Add(new RolePermission { RoleId = rol.Id, PermissionId = izin.Id, SourceBundleId = paket.Id, GrantedAt = Simdi });
        await db.SaveChangesAsync();
        var once = await db.RolePermissions.CountAsync();

        var sonuc = await RolIzinKatalogu.EsitleAsync(db, Simdi);

        Assert.Equal(0, sonuc.Eklenen);
        Assert.Equal(once, await db.RolePermissions.CountAsync());
    }
}
