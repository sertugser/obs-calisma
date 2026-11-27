using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds default Ders Kategorisi values. Admin can extend in future.
/// </summary>
public static class CourseCategorySeed
{
    public static readonly IReadOnlyList<CourseCategory> All = new List<CourseCategory>
    {
        new() { Code = "Default",       Name = "Varsayılan",   SortOrder = 10, IsActive = true },
        new() { Code = "Compulsory",    Name = "Zorunlu",      SortOrder = 20, IsActive = true },
        new() { Code = "Elective",      Name = "Seçmeli",      SortOrder = 30, IsActive = true },
        new() { Code = "Common",        Name = "Ortak",        SortOrder = 40, IsActive = true },
        new() { Code = "ServiceCourse", Name = "Servis Dersi", SortOrder = 50, IsActive = true },
    };

    public static async Task SeedAsync(AppDbContext context)
    {
        // Katalog kuralı: eksik kategori eklenir, yöneticinin sildiği geri gelmez, var olana dokunulmaz.
        // Liste ortak (static) nesneler tuttuğu için her ekleme yeni bir kopyadır.
        await Katalog.KatalogEsitleyici.EsitleAsync(context, new Katalog.KatalogTuru<CourseCategory, CourseCategory>
        {
            Ad = "ders-kategorisi",
            Kayitlar = All,
            KayitAnahtari = c => c.Code,
            VarlikAnahtari = c => c.Code,
            Olustur = (c, simdi) => new CourseCategory { Code = c.Code, Name = c.Name, IsActive = c.IsActive, SortOrder = c.SortOrder, CreatedAt = simdi },
        }, DateTime.UtcNow);
    }
}
