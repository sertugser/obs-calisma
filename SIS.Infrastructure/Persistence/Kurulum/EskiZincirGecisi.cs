using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SIS.Infrastructure.Persistence.Kurulum;

// Adım 3.10: 151 migration'lık eski zincir tek "Baslangic" migration'ına indirildi. Eski zincirin
// sonuna kadar kurulmuş bir veritabanının şeması Baslangic ile aynıdır; böyle bir veritabanında
// geçmiş tablosu tek Baslangic satırına çevrilir, sonraki migration'lar olağan yoldan uygulanır.
// Eski zincirin ortasında kalmış veritabanı çevrilmez: önce eski sürümle sona kadar güncellenmelidir.
public static class EskiZincirGecisi
{
    public const string EskiZincirinSonu = "20261126192307_MenuOgesiSlugTekil";
    public const string Baslangic = "20261128000444_Baslangic";

    // Geçiş yapıldıysa true döner. Veritabanı yoksa ya da geçmiş boşsa hiçbir şey yapmaz.
    public static async Task<bool> UygulaAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (!await db.Database.CanConnectAsync(ct)) return false;

        var gecmis = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
        if (gecmis.Count == 0 || gecmis.Contains(Baslangic)) return false;

        var son = gecmis.Max(StringComparer.Ordinal);
        if (son != EskiZincirinSonu)
            throw new InvalidOperationException(
                $"Veritabanı eski migration zincirinin sonunda değil (son uygulanan: {son}). " +
                $"Önce önceki sürümle {EskiZincirinSonu} migration'ına kadar güncelleyin.");

        await using var islem = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("""DELETE FROM "__EFMigrationsHistory";""", ct);
        await db.Database.ExecuteSqlRawAsync(
            """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ({0}, {1});""",
            [Baslangic, ProductInfo.GetVersion()], ct);
        await islem.CommitAsync(ct);
        return true;
    }
}
