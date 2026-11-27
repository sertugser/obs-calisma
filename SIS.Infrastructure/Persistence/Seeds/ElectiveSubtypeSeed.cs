using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// Faz B B3a — DB-driven seçmeli ders tipleri seed'i.
/// Katalog kuralı: Code'su tabloda da katalog kaydında da olmayanı ekler; mevcut kayıtlara dokunmaz, yöneticinin sildiği geri gelmez.
/// Eski not:
/// mevcut kayıtlara dokunmaz (admin'in Name/SortOrder gibi değişikliklerini koruma garantisi).
/// </summary>
public static class ElectiveSubtypeSeed
{
    public static readonly ElectiveSubtypeEntity[] Defaults =
    {
        new()
        {
            Code = "UniversityElective", Name = "Üniversite Seçmeli",
            RequiresFaculty = false, ForbidsFaculty = true,
            RequiresDepartment = false, ForbidsDepartment = true,
            IsActive = true, SortOrder = 1,
        },
        new()
        {
            Code = "NonDepartmentElective", Name = "Bölüm Dışı Seçmeli",
            RequiresFaculty = false, ForbidsFaculty = false,
            RequiresDepartment = false, ForbidsDepartment = false,
            IsActive = true, SortOrder = 2,
        },
        new()
        {
            Code = "TechnicalElective", Name = "Teknik Seçmeli",
            RequiresFaculty = true, ForbidsFaculty = false,
            RequiresDepartment = false, ForbidsDepartment = false,
            IsActive = true, SortOrder = 3,
        },
    };

    /// <summary>
    /// Katalog kuralı: Code'su tabloda da katalog kaydında da olmayanı ekler; mevcut kayıtlara dokunmaz, yöneticinin sildiği geri gelmez.
    /// </summary>
    public static async Task SeedAsync(AppDbContext context, CancellationToken ct = default)
    {
        await Katalog.KatalogEsitleyici.EsitleAsync(context, new Katalog.KatalogTuru<ElectiveSubtypeEntity, ElectiveSubtypeEntity>
        {
            Ad = "secmeli-alt-turu",
            Kayitlar = Defaults,
            KayitAnahtari = d => d.Code,
            VarlikAnahtari = d => d.Code,
            Olustur = (d, simdi) => new ElectiveSubtypeEntity
            {
                Code = d.Code,
                Name = d.Name,
                RequiresFaculty = d.RequiresFaculty,
                ForbidsFaculty = d.ForbidsFaculty,
                RequiresDepartment = d.RequiresDepartment,
                ForbidsDepartment = d.ForbidsDepartment,
                IsActive = d.IsActive,
                SortOrder = d.SortOrder,
                CreatedAt = simdi,
            },
        }, DateTime.UtcNow, ct);
    }
}
