using Microsoft.AspNetCore.Identity;
using SIS.Domain.Enums;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence.Seeds;

namespace SIS.Infrastructure.Persistence.Katalog;

// Her kurulumda gereken sistem verisinin tek tanımı. Şimdilik roller ve izinler; öteki türler
// Adım 3'ün sonraki paketlerinde eklenir.
public static class SistemKatalogu
{
    public static readonly IReadOnlyList<string> RolAdlari =
    [
        "Student", "Instructor", "AcademicAdvisor",
        "AdministrativeStaff", "DepartmentManager", "Dean", "SystemAdministrator",
    ];

    // Rol ekranı rol yaratmıyor ve silmiyor; kataloğun ad dışında alanı yok.
    public static readonly KatalogTuru<IdentityRole<int>, string> Roller = new()
    {
        Ad = "rol",
        Kayitlar = RolAdlari,
        KayitAnahtari = ad => ad,
        VarlikAnahtari = rol => rol.Name ?? string.Empty,
        Olustur = (ad, _) => new IdentityRole<int>
        {
            Name = ad,
            NormalizedName = ad.ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        },
    };

    // İzin ekranı izin alanı yazmıyor; kategori ve açıklama kataloğundur.
    public static readonly KatalogTuru<Permission, (string Key, PermissionCategory Category, string Description)> Izinler = new()
    {
        Ad = "izin",
        Kayitlar = PermissionSeed.All,
        KayitAnahtari = i => i.Key,
        VarlikAnahtari = p => p.Key,
        Olustur = (i, simdi) => new Permission
        {
            Key = i.Key,
            Category = i.Category,
            Description = i.Description,
            CreatedAt = simdi,
        },
        KatalogAlanlariniYaz = (p, i) =>
        {
            if (p.Category == i.Category && p.Description == i.Description) return false;
            p.Category = i.Category;
            p.Description = i.Description;
            return true;
        },
    };

    public static async Task<IReadOnlyList<KatalogSonucu>> EsitleAsync(AppDbContext db, DateTime simdi, CancellationToken ct = default) =>
    [
        await KatalogEsitleyici.EsitleAsync(db, Roller, simdi, ct),
        await KatalogEsitleyici.EsitleAsync(db, Izinler, simdi, ct),
    ];
}
