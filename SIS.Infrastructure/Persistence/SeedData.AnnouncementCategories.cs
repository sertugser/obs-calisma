using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedAnnouncementCategoriesAsync(AppDbContext context)
    {
        // Sistem kategorileri — silinemez, sadece görsel özellikleri admin tarafından düzenlenebilir.
        // Renkler Tailwind palette'inden alındı (oklch değil HEX, frontend de aynı kodu kullanır).
        var defaults = new List<AnnouncementCategory>
        {
            new() { Name = "Akademik", ColorHex = "#3b82f6", IconName = "GraduationCap", SortOrder = 10, IsSystem = true, CreatedAt = DateTime.UtcNow },
            new() { Name = "İdari",    ColorHex = "#64748b", IconName = "Building2",     SortOrder = 20, IsSystem = true, CreatedAt = DateTime.UtcNow },
            new() { Name = "Sosyal",   ColorHex = "#ec4899", IconName = "Sparkles",      SortOrder = 30, IsSystem = true, CreatedAt = DateTime.UtcNow },
            new() { Name = "Ders",     ColorHex = "#10b981", IconName = "BookOpen",      SortOrder = 40, IsSystem = true, CreatedAt = DateTime.UtcNow },
        };

        // Katalog kuralı (anahtar: ad). Sistem kategorileri silinemez; ilk eşitlemeden sonra yönetici adını
        // değiştirirse katalog yeni kategori eklemez (eski ad kayıtta olduğu için atlanır).
        await Katalog.KatalogEsitleyici.EsitleAsync(context, new Katalog.KatalogTuru<AnnouncementCategory, AnnouncementCategory>
        {
            Ad = "duyuru-kategorisi",
            Kayitlar = defaults,
            KayitAnahtari = c => c.Name,
            VarlikAnahtari = c => c.Name,
            Olustur = (c, _) => c,
        }, DateTime.UtcNow);
    }
}
