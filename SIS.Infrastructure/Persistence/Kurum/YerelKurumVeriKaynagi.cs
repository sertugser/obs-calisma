using SIS.Infrastructure.Persistence.Seeds;

namespace SIS.Infrastructure.Persistence.Kurum;

// Bir kurumun kendine özgü verisi: fakülte, bölüm, program, müfredat, ders, derslik ve kurumun
// ek sözlük kayıtları. Çekirdek bu veriyi içermez; `setup --kurum <ad>` ile yazılır.
public interface IKurumVeriKaynagi
{
    string Ad { get; }
    Task YazAsync(AppDbContext db, CancellationToken ct = default);
}

// Bu depoda duran tek kurum verisi (bugünkü akademik yapı, dersler ve derslikler). Adım 4'te kurum
// paketine taşınır. Her adım "boşsa yaz" ya da katalog kuralıyla çalıştığı için tekrar çağrılabilir.
public sealed class YerelKurumVeriKaynagi : IKurumVeriKaynagi
{
    public const string KaynakAdi = "yerel";

    public string Ad => KaynakAdi;

    public async Task YazAsync(AppDbContext db, CancellationToken ct = default)
    {
        await SeedData.SeedAcademicStructureAsync(db);
        await SeedData.SeedCoursesAsync(db);
        await SeedData.SeedClassroomsAsync(db);
        await CurriculumCourseOpeningReasonSeed.YerelKurumuSeedAsync(db);
    }
}

public static class KurumVeriKaynaklari
{
    public static IReadOnlyList<IKurumVeriKaynagi> Hepsi { get; } = [new YerelKurumVeriKaynagi()];

    public static IKurumVeriKaynagi Bul(string ad) =>
        Hepsi.FirstOrDefault(k => string.Equals(k.Ad, ad, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"'{ad}' adlı kurum verisi yok. Var olanlar: {string.Join(", ", Hepsi.Select(k => k.Ad))}");
}
