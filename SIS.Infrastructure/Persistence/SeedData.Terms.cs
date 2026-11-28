using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    // Örnek veri dönemleri "bugün"e göre kurulur (Adım 3.7a): son dört akademik yılın güz ve bahar
    // dönemleri ile bugünü içeren (ya da en son başlamış) dönem. Dönem (yıl, tür) ile tanınır; var
    // olanlara dokunulmaz (ör. migration'ın eklediği yaz dönemi). Her yeni döneme bir akademik takvim
    // yazılır.
    // Örnek veri dönemleri: son dört akademik yılın güz ve bahar dönemleri, gelecekte başlayanlar hariç.
    internal static List<(int Yil, TermType Tur, DateTime Bas, DateTime Bit)> DemoDonemleri(DateTime bugun)
    {
        var baslangicYili = bugun.Month >= 8 ? bugun.Year : bugun.Year - 1;
        var donemler = new List<(int Yil, TermType Tur, DateTime Bas, DateTime Bit)>();
        for (var yil = baslangicYili - 4; yil <= baslangicYili; yil++)
        {
            donemler.Add((yil, TermType.Fall, Utc(yil, 9, 15), Utc(yil + 1, 1, 10)));
            donemler.Add((yil, TermType.Spring, Utc(yil + 1, 2, 15), Utc(yil + 1, 6, 10)));
        }
        return donemler.Where(d => d.Bas <= bugun.Date).OrderBy(d => d.Bas).ToList();
    }

    // Bugünü içeren ya da en son başlamış örnek dönem (dönem aralarında bitmiş dönem).
    public static (int Yil, TermType Tur) GuncelDemoDonemi(DateTime bugun)
    {
        var son = DemoDonemleri(bugun).Last();
        return (son.Yil, son.Tur);
    }

    internal static async Task SeedTermsAsync(AppDbContext context, DateTime bugun)
    {
        var donemler = DemoDonemleri(bugun);
        var guncel = donemler.Last();

        var varOlan = (await context.AcademicTerms.Select(t => new { t.Year, t.TermType }).ToListAsync())
            .Select(t => (t.Year, t.TermType)).ToHashSet();
        var yeniler = donemler.Where(d => !varOlan.Contains((d.Yil, d.Tur))).ToList();
        foreach (var d in yeniler)
        {
            var (ad, kisa, ingilizce) = d.Tur == TermType.Fall
                ? ($"{d.Yil}-{d.Yil + 1} Güz", $"{d.Yil % 100:00}-{(d.Yil + 1) % 100:00} Güz", $"Fall {d.Yil}-{d.Yil + 1}")
                : ($"{d.Yil}-{d.Yil + 1} Bahar", $"{d.Yil % 100:00}-{(d.Yil + 1) % 100:00} Bahar", $"Spring {d.Yil}-{d.Yil + 1}");
            context.AcademicTerms.Add(new AcademicTerm
            {
                Name = ad,
                ShortName = kisa,
                EnglishName = ingilizce,
                Year = d.Yil,
                TermType = d.Tur,
                TermStart = d.Bas,
                TermEnd = d.Bit,
                YoksisStatus = YoksisStatus.Inactive,
                CreatedAt = bugun,
                AcademicCalendar = new TermAcademicCalendar
                {
                    RegistrationStart = d.Bas.AddDays(-14),
                    RegistrationEnd = d.Bas.AddDays(-7),
                    AddDropStart = d.Bas.AddDays(7),
                    AddDropEnd = d.Bas.AddDays(14),
                    WithdrawalDeadline = d.Bas.AddDays(56),
                    GradeEntryStart = d.Bit.AddDays(-14),
                    GradeEntryEnd = d.Bit.AddDays(14),
                    CreatedAt = bugun,
                },
            });
        }
        await context.SaveChangesAsync();
        await SeedTermCalendarEventsAsync(context, yeniler);

        // Taze örnek veride (bu dönemlerin hiçbiri yoktu) tek etkin dönem bugünkü dönemdir; migration'ın
        // eklediği yaz dönemi gibi önceden etkin görünen dönem kapatılır. Örnek veri sonradan yeniden
        // çalışırsa (yeni bir dönem başlamış olsa bile) yöneticinin seçtiği etkin döneme dokunulmaz.
        if (yeniler.Count == donemler.Count)
        {
            foreach (var donem in await context.AcademicTerms.Where(t => t.IsActivated).ToListAsync())
                donem.IsActivated = false;
            (await context.AcademicTerms.SingleAsync(t => t.Year == guncel.Yil && t.TermType == guncel.Tur)).IsActivated = true;

            await TazeOrnekVerideSaatDiliminiAtaAsync(context);
            await context.SaveChangesAsync();
        }
    }

    private const string OrnekKurumSaatDilimi = "Europe/Istanbul";

    // Ürün kararı: varsayılan dünya saati (Etc/UTC), örnek veride örnek kurumun dilimi. Yalnız taze örnek
    // veride çağrılır ve değer hâlâ katalog varsayılanıysa atanır; kurulumdan önce düz kurulum yapılmış ya
    // da önceki bir koşu yarıda kalmış olması fark etmez. Yöneticinin seçtiği başka bir dilime dokunulmaz;
    // örnek veri yeniden kurulurken bu yol çalışmadığından sonradan kaydedilmiş Etc/UTC de ezilmez.
    // Anahtar silinmişse atama yapılmaz (katalog kuralı: yöneticinin sildiği geri gelmez).
    private static async Task TazeOrnekVerideSaatDiliminiAtaAsync(AppDbContext context)
    {
        var ayar = await context.SystemConfigs.FirstOrDefaultAsync(c => c.Key == InstitutionTimeZoneValidator.ConfigKey);
        if (ayar?.Value == InstitutionTimeZoneValidator.DefaultId)
            ayar.Value = OrnekKurumSaatDilimi;
    }

    // Derslikler kurum verisidir (Kurum/YerelKurumVeriKaynagi).
    internal static async Task SeedClassroomsAsync(AppDbContext context)
    {
        // Classrooms
        if (!context.Classrooms.Any())
        {
            var classrooms = new (string Name, int Capacity, ClassroomType Type)[]
            {
                ("AMFİ 1", 110, ClassroomType.Lecture),
                ("Amfi - Sınıf-Yeni-Kat Sıfır", 80, ClassroomType.Lecture),
                ("C 017", 70, ClassroomType.Lecture),
                ("C 018", 70, ClassroomType.Lecture),
                ("C 025", 70, ClassroomType.Lecture),
                ("C 026", 70, ClassroomType.Lecture),
                ("C 036", 80, ClassroomType.Lecture),
                ("C 054", 30, ClassroomType.Lecture),
                ("C 005", 50, ClassroomType.Lecture),
                ("L139 Ceng Lab-1", 40, ClassroomType.Lab),
                ("L141 Ceng Lab-2", 40, ClassroomType.Lab),
                ("Ceng Lab-3 (Kat 0)", 40, ClassroomType.Lab),
                ("L140 EEE LAB", 40, ClassroomType.Lab),
                ("L143 PHY LAB", 40, ClassroomType.Lab),
            };

            foreach (var (name, capacity, type) in classrooms)
            {
                context.Classrooms.Add(new Classroom { Name = name, Capacity = capacity, Type = type });
            }
            await context.SaveChangesAsync();
        }
    }

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
