using Microsoft.Extensions.Caching.Memory;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.CalendarGenerator;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    // Örnek veri, paket 35: yeni örnek dönemlere takvim olayları sistemin kendi takvim üreticisiyle
    // (varsayılan şablonlar ve tatiller) yazılır; yönetim ekranındaki "takvim oluştur ve uygula" ile aynı
    // yol. Dönem tarihleri ve akademik takvim olaylardan güncellenir. Olaylar olmadan takvime bağlı
    // modüller "Takvim Eksik" ile kapalı kalıyordu. Yalnız bu çağrıda yazılan dönemlere uygulanır; bu
    // paketten önce kurulmuş örnek dönemlere olay eklenmez (yönetim ekranından uygulanabilir).
    public static async Task SeedTermCalendarEventsAsync(
        AppDbContext context, IEnumerable<(int Yil, TermType Tur, DateTime Bas, DateTime Bit)> yeniDonemler)
    {
        var sablonlar = new CalendarTemplateService(context);
        var uretici = new CalendarGeneratorService(
            context,
            sablonlar,
            new HolidayCalculatorService(context, sablonlar),
            new AcademicCalendarRuleService(context),
            new TimeSlotService(context, new SystemConfigProvider(context, new MemoryCache(new MemoryCacheOptions()))));

        foreach (var yil in yeniDonemler.GroupBy(d => d.Yil).OrderBy(g => g.Key))
        {
            var guz = yil.Where(d => d.Tur == TermType.Fall).Select(d => (DateTime?)d.Bas).FirstOrDefault();
            var bahar = yil.Where(d => d.Tur == TermType.Spring).Select(d => (DateTime?)d.Bas).FirstOrDefault();
            var taslak = await uretici.GenerateAsync(new GenerateCalendarRequestDto(
                $"{yil.Key}-{yil.Key + 1}", guz, bahar, IncludeSummer: false, Constraints: null, TemplateDurationOverrides: null));

            var yariyillar = yil.Select(d => d.Tur == TermType.Fall ? SemesterType.Fall : SemesterType.Spring).ToList();
            await uretici.ApplyAsync(taslak.DraftId, new ApplyCalendarDraftRequestDto(ForceOverwrite: false, Semesters: yariyillar), userId: null);
        }
    }
}
