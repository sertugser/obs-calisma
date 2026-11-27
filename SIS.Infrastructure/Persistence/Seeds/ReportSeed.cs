using Microsoft.EntityFrameworkCore;
using SIS.Domain.Identity;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// Handler'ı olan raporları DB'ye idempotent ekler. Code = handler.Code ile birebir.
/// Kategori 'genel-raporlar' altında.
/// </summary>
public static class ReportSeed
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var category = await db.ReportCategories.FirstOrDefaultAsync(c => c.Code == "genel-raporlar", ct);
        if (category is null)
        {
            category = new ReportCategory { Code = "genel-raporlar", Name = "Genel Raporlar" };
            db.ReportCategories.Add(category);
            await db.SaveChangesAsync(ct);
        }

        // Handler'ı olan raporlar (Code = handler.Code). Yeni handler eklerken buraya bir satır ekle.
        var definitions = new (string Code, string Name, string Description)[]
        {
            ("ogrenci-listesi", "Öğrenci Listesi", "Duruma, programa ve GNO aralığına göre öğrenci listesi."),
            ("mezun-aday-listesi", "Mezun Aday Listesi", "Tamamlanan yarıyıl ve GNO eşiğini geçen aktif öğrenciler."),
            ("ders-kayit-listesi", "Ders Kayıt Listesi", "Bir dönem/şubedeki ders kayıtları (öğrenci, ders, durum, not)."),
            ("danisman-ogrenci-listesi", "Danışman Öğrenci Listesi", "Bir danışmana atanmış öğrenciler."),
            ("sube-kontenjan-listesi", "Şube/Kontenjan Listesi", "Şubelerin kapasite ve doluluk durumu."),
            ("not-durum-cizelgesi", "Not Durum Çizelgesi", "Bir dönem/şubedeki öğrencilerin komponent puanları (vize/final/ödev…)."),
        };

        await Katalog.KatalogEsitleyici.EsitleAsync(db, new Katalog.KatalogTuru<ReportDefinition, (string Code, string Name, string Description)>
        {
            Ad = "rapor",
            Kayitlar = definitions,
            KayitAnahtari = d => d.Code,
            VarlikAnahtari = r => r.Code,
            Olustur = (d, _) => new ReportDefinition { Code = d.Code, Name = d.Name, Description = d.Description, CategoryId = category.Id },
        }, DateTime.UtcNow, ct);
    }
}
