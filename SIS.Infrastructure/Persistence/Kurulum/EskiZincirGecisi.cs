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
    public const int EskiZincirUzunlugu = 151;
    public const string Baslangic = "20261128000444_Baslangic";

    // Geçiş yapıldıysa true döner. Veritabanı yoksa ya da geçmiş boşsa hiçbir şey yapmaz.
    public static async Task<bool> UygulaAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (!await db.Database.CanConnectAsync(ct)) return false;
        if (!await GecisGerekirAsync(db, ct)) return false;

        // Aynı anda çalışan iki kurulum geçmişi birlikte yazmasın: tablo kilitlenir, karar kilit altında yinelenir.
        await using var islem = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("""LOCK TABLE "__EFMigrationsHistory" IN EXCLUSIVE MODE;""", ct);
        if (!await GecisGerekirAsync(db, ct)) return false;

        await db.Database.ExecuteSqlRawAsync("""DELETE FROM "__EFMigrationsHistory";""", ct);
        await db.Database.ExecuteSqlRawAsync(
            """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ({0}, {1});""",
            [Baslangic, ProductInfo.GetVersion()], ct);
        await islem.CommitAsync(ct);
        return true;
    }

    private static async Task<bool> GecisGerekirAsync(AppDbContext db, CancellationToken ct)
    {
        var gecmis = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
        if (gecmis.Count == 0 || gecmis.Contains(Baslangic)) return false;

        var son = gecmis.Max(StringComparer.Ordinal);
        if (son != EskiZincirinSonu || gecmis.Count != EskiZincirUzunlugu)
            throw new InvalidOperationException(
                $"Veritabanı eski migration zincirinin sonunda değil (uygulanan {gecmis.Count}, son: {son}). " +
                $"Önce önceki sürümle {EskiZincirinSonu} migration'ına kadar güncelleyin.");
        return true;
    }
}
