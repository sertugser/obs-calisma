using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Application.CalendarGenerator.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services.CalendarGenerator;

public class CalendarTemplateService : ICalendarTemplateService
{
    private readonly AppDbContext _context;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        // Keep non-ASCII chars (Turkish ş, ğ, ü, ç, ı, ö) as-is instead of \uXXXX;
        // PostgreSQL's json type rejects some \u escape sequences (error 22P05).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public CalendarTemplateService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CalendarTemplateDto>> GetTemplatesAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);

        var templates = await _context.CalendarTemplates
            .AsNoTracking()
            .OrderBy(t => t.ChainCode)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        return templates.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<CalendarTemplateDto>> ReplaceTemplatesAsync(IReadOnlyList<UpsertCalendarTemplateDto> templates, CancellationToken ct = default)
    {
        var existing = await _context.CalendarTemplates.ToListAsync(ct);
        _context.CalendarTemplates.RemoveRange(existing);

        var now = DateTime.UtcNow;
        foreach (var dto in templates)
        {
            _context.CalendarTemplates.Add(new CalendarTemplate
            {
                Name = dto.Name.Trim(),
                ChainCode = dto.ChainCode.Trim().ToUpperInvariant(),
                IsActive = dto.IsActive,
                ItemsJson = JsonSerializer.Serialize(dto.Items.OrderBy(i => i.SortOrder), JsonOptions),
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync(ct);
        return await GetTemplatesAsync(ct);
    }

    public async Task<IReadOnlyList<HolidayDefinitionDto>> GetHolidayDefinitionsAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);

        var definitions = await _context.HolidayDefinitions
            .AsNoTracking()
            .OrderBy(h => h.Type)
            .ThenBy(h => h.FixedMonth ?? 99)
            .ThenBy(h => h.FixedDay ?? 99)
            .ThenBy(h => h.Name)
            .ToListAsync(ct);

        return definitions.Select(ToDto).ToList();
    }

    public async Task<HolidayDefinitionDto> UpsertHolidayDefinitionAsync(int? id, UpsertHolidayDefinitionDto dto, CancellationToken ct = default)
    {
        HolidayDefinition entity;
        var now = DateTime.UtcNow;

        if (id.HasValue)
        {
            entity = await _context.HolidayDefinitions.FindAsync([id.Value], ct)
                ?? throw new KeyNotFoundException($"Holiday definition not found: {id.Value}");
            entity.UpdatedAt = now;
        }
        else
        {
            entity = new HolidayDefinition { CreatedAt = now };
            _context.HolidayDefinitions.Add(entity);
        }

        entity.Name = dto.Name.Trim();
        entity.EnglishName = dto.EnglishName.Trim();
        entity.Type = dto.Type;
        entity.FixedMonth = dto.FixedMonth;
        entity.FixedDay = dto.FixedDay;
        entity.HijriMonth = dto.HijriMonth;
        entity.HijriDay = dto.HijriDay;
        entity.DurationDays = dto.DurationDays;
        entity.IsHalfDayStart = dto.IsHalfDayStart;
        entity.IsOfficialHoliday = dto.IsOfficialHoliday;
        entity.BlocksAcademicEvents = dto.BlocksAcademicEvents;
        entity.Source = dto.Source.Trim();
        entity.OverridesJson = JsonSerializer.Serialize(dto.Overrides, JsonOptions);
        entity.IsActive = dto.IsActive;

        await _context.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    // Tablo boşsa varsayılan şablonları ve tatilleri yazar; doluysa hiçbir satıra dokunmaz.
    // Okuma yolları bunu çağırır: yöneticinin düzenlediği şablon ve tatiller okurken ezilmez.
    public async Task EnsureDefaultsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        if (!await _context.CalendarTemplates.AnyAsync(ct))
        {
            _context.CalendarTemplates.AddRange(DefaultTemplates(now));
            await _context.SaveChangesAsync(ct);
        }

        if (!await _context.HolidayDefinitions.AnyAsync(ct))
        {
            _context.HolidayDefinitions.AddRange(DefaultHolidayDefinitions(now));
            await _context.SaveChangesAsync(ct);
        }
    }

    // Yalnız kurulum (setup) çağırır: boşsa varsayılanları yazar, doluysa yönetmelik uyumu ve
    // yerelleştirilmiş varsayılan adlar için bilinen eski varsayılan satırları günceller.
    public async Task KurulumdaVarsayilanlariUygulaAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var sablonVardi = await _context.CalendarTemplates.AnyAsync(ct);
        var tatilVardi = await _context.HolidayDefinitions.AnyAsync(ct);
        await EnsureDefaultsAsync(ct);

        if (sablonVardi)
        {
            await EnsureRegulationAlignedDefaultsAsync(now, ct);
            await EnsureLocalizedDefaultLabelsAsync(now, ct);
        }

        if (tatilVardi)
            await EnsureLocalizedHolidayLabelsAsync(now, ct);
    }

    private async Task EnsureRegulationAlignedDefaultsAsync(DateTime now, CancellationToken ct)
    {
        var mainTemplate = await _context.CalendarTemplates.FirstOrDefaultAsync(t => t.ChainCode == "MAIN", ct);
        if (mainTemplate != null)
        {
            var items = DeserializeItems(mainTemplate.ItemsJson);
            var midterm = items.FirstOrDefault(i => i.Key == "midterm_exam");
            var final = items.FirstOrDefault(i => i.Key == "final_exam");
            var addDrop = items.FirstOrDefault(i => i.Key == "add_drop");

            var requiresMainAlignment =
                midterm == null
                || midterm.DefaultDurationDays != 5
                || midterm.OffsetDays != 49
                || final == null
                || final.DefaultDurationDays != 12
                || final.OffsetDays != 105
                || addDrop == null
                || addDrop.DefaultDurationDays != 7;

            if (requiresMainAlignment)
            {
                mainTemplate.ItemsJson = SerializeItems(DefaultMainItems());
                mainTemplate.UpdatedAt = now;
                await _context.SaveChangesAsync(ct);
            }
        }

        var summerTemplate = await _context.CalendarTemplates.FirstOrDefaultAsync(t => t.ChainCode == "SUMMER", ct);
        if (summerTemplate == null)
            return;

        var summerItems = DeserializeItems(summerTemplate.ItemsJson);
        var classesEnd = summerItems.FirstOrDefault(i => i.Key == "summer_classes_end");
        var exam = summerItems.FirstOrDefault(i => i.Key == "summer_exam");

        if (classesEnd != null && exam != null && exam.OffsetDays <= classesEnd.OffsetDays)
        {
            summerTemplate.ItemsJson = SerializeItems(DefaultSummerItems());
            summerTemplate.UpdatedAt = now;
            await _context.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureLocalizedDefaultLabelsAsync(DateTime now, CancellationToken ct)
    {
        var templates = await _context.CalendarTemplates
            .Where(t => t.ChainCode == "MAIN" || t.ChainCode == "ADMISSIONS" || t.ChainCode == "SUMMER")
            .ToListAsync(ct);
        if (templates.Count == 0)
            return;

        var defaultTitles = DefaultMainItems()
            .Concat(DefaultAdmissionsItems())
            .Concat(DefaultSummerItems())
            .ToDictionary(i => i.Key, i => i.Title, StringComparer.OrdinalIgnoreCase);

        var templateNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MAIN"] = "Ana Akademik Süreç",
            ["ADMISSIONS"] = "Başvuru ve Kayıt Süreçleri",
            ["SUMMER"] = "Yaz Öğretimi"
        };

        var changed = false;
        foreach (var template in templates)
        {
            if (templateNames.TryGetValue(template.ChainCode, out var templateName) && template.Name != templateName)
            {
                template.Name = templateName;
                template.UpdatedAt = now;
                changed = true;
            }

            var items = DeserializeItems(template.ItemsJson);
            var localizedItems = items
                .Select(item => defaultTitles.TryGetValue(item.Key, out var title) && item.Title != title ? item with { Title = title } : item)
                .ToList();

            if (!items.SequenceEqual(localizedItems))
            {
                template.ItemsJson = SerializeItems(localizedItems);
                template.UpdatedAt = now;
                changed = true;
            }
        }

        if (changed)
            await _context.SaveChangesAsync(ct);
    }

    private async Task EnsureLocalizedHolidayLabelsAsync(DateTime now, CancellationToken ct)
    {
        var holidayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["New Year's Day"] = "Yılbaşı Tatili",
            ["National Sovereignty and Children's Day"] = "Ulusal Egemenlik ve Çocuk Bayramı",
            ["Labour and Solidarity Day"] = "Emek ve Dayanışma Günü",
            ["Commemoration of Ataturk, Youth and Sports Day"] = "Atatürk'ü Anma, Gençlik ve Spor Bayramı",
            ["Democracy and National Unity Day"] = "Demokrasi ve Milli Birlik Günü",
            ["Victory Day"] = "Zafer Bayramı",
            ["Republic Day"] = "Cumhuriyet Bayramı",
            ["Ataturk Memorial Day"] = "Atatürk'ü Anma Günü",
            ["Eid al-Fitr"] = "Ramazan Bayramı",
            ["Eid al-Adha"] = "Kurban Bayramı"
        };

        var holidays = await _context.HolidayDefinitions.ToListAsync(ct);
        var removed = holidays
            .Where(holiday =>
                holiday.Type == HolidayType.ImportantDay
                && (
                    holiday.EnglishName.Equals("Ataturk Memorial Day", StringComparison.OrdinalIgnoreCase)
                    || holiday.Name.Equals("Atatürk'ü Anma Günü", StringComparison.OrdinalIgnoreCase)
                ))
            .ToList();

        if (removed.Count > 0)
        {
            _context.HolidayDefinitions.RemoveRange(removed);
            holidays = holidays.Except(removed).ToList();
        }

        var changed = false;
        foreach (var holiday in holidays)
        {
            if (!holidayNames.TryGetValue(holiday.EnglishName, out var name) || holiday.Name == name)
                continue;

            holiday.Name = name;
            holiday.UpdatedAt = now;
            changed = true;
        }

        if (removed.Count > 0 || changed)
            await _context.SaveChangesAsync(ct);
    }

    private static CalendarTemplateDto ToDto(CalendarTemplate entity)
    {
        var items = DeserializeItems(entity.ItemsJson);
        return new CalendarTemplateDto(entity.Id, entity.Name, entity.ChainCode, entity.IsActive, items);
    }

    private static HolidayDefinitionDto ToDto(HolidayDefinition entity)
    {
        var overrides = DeserializeOverrides(entity.OverridesJson);
        return new HolidayDefinitionDto(
            entity.Id,
            entity.Name,
            entity.EnglishName,
            entity.Type,
            entity.FixedMonth,
            entity.FixedDay,
            entity.HijriMonth,
            entity.HijriDay,
            entity.DurationDays,
            entity.IsHalfDayStart,
            entity.IsOfficialHoliday,
            entity.BlocksAcademicEvents,
            entity.Source,
            overrides,
            entity.IsActive);
    }

    internal static IReadOnlyList<CalendarTemplateItemDto> DeserializeItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<CalendarTemplateItemDto>>(json, JsonOptions) ?? [];
    }

    internal static IReadOnlyList<HolidayOverrideDto> DeserializeOverrides(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        if (json.TrimStart().StartsWith("{"))
        {
            var map = JsonSerializer.Deserialize<Dictionary<int, HolidayOverrideDto>>(json, JsonOptions);
            return map?.Values.OrderBy(v => v.Year).ToList() ?? [];
        }

        return JsonSerializer.Deserialize<List<HolidayOverrideDto>>(json, JsonOptions) ?? [];
    }

    internal static string SerializeItems(IEnumerable<CalendarTemplateItemDto> items) =>
        JsonSerializer.Serialize(items.OrderBy(i => i.SortOrder), JsonOptions);

    internal static string SerializeOverrides(IEnumerable<HolidayOverrideDto> overrides) =>
        JsonSerializer.Serialize(overrides.OrderBy(o => o.Year), JsonOptions);

    private static IEnumerable<CalendarTemplate> DefaultTemplates(DateTime now)
    {
        return
        [
            new()
            {
                Name = "Ana Akademik Süreç",
                ChainCode = "MAIN",
                IsActive = true,
                ItemsJson = SerializeItems(DefaultMainItems()),
                CreatedAt = now
            },
            new()
            {
                Name = "Başvuru ve Kayıt Süreçleri",
                ChainCode = "ADMISSIONS",
                IsActive = true,
                ItemsJson = SerializeItems(DefaultAdmissionsItems()),
                CreatedAt = now
            },
            new()
            {
                Name = "Yaz Öğretimi",
                ChainCode = "SUMMER",
                IsActive = true,
                ItemsJson = SerializeItems(DefaultSummerItems()),
                CreatedAt = now
            }
        ];
    }

    private static IEnumerable<CalendarTemplateItemDto> DefaultMainItems()
    {
        return
        [
            Item("classes_start", CalendarEventType.ClassesStart, "Derslerin Başlaması", 1, 0, null, SemesterType.Fall, SemesterType.Spring),
            Item("add_drop", CalendarEventType.AddDrop, "Ders Ekleme - Bırakma, Geç Kayıtlar ve Danışman Onayları", 7, 7, null, SemesterType.Fall, SemesterType.Spring),
            Item("withdrawal_deadline", CalendarEventType.CourseWithdrawal, "Dersten Çekilme Başvurularının Son Günü", 1, 40, null, SemesterType.Fall, SemesterType.Spring),
            Item("freeze_deadline", CalendarEventType.RegistrationFreezeDeadline, "Kayıt Dondurma Başvurularının Son Günü", 1, 40, null, SemesterType.Fall, SemesterType.Spring),
            Item("midterm_exam", CalendarEventType.MidtermExam, "Ara Sınavlar", 5, 49, null, SemesterType.Fall, SemesterType.Spring),
            Item("midterm_excuse_application", CalendarEventType.ExcuseExamApplication, "Mazeret Sınav Başvurularının Son Günü (Ara Sınav)", 1, 50, null, SemesterType.Fall, SemesterType.Spring),
            Item("midterm_excuse_exam", CalendarEventType.ExcuseExam, "Mazeret Sınavları (Ara Sınav)", 3, 52, null, SemesterType.Fall, SemesterType.Spring),
            Item("midterm_publication", CalendarEventType.MidtermGradePublication, "Ara Sınavların İlanının Son Günü", 1, 60, null, SemesterType.Fall, SemesterType.Spring),
            Item("classes_end", CalendarEventType.ClassesEnd, "Derslerin Sona Ermesi", 1, 102, null, SemesterType.Fall, SemesterType.Spring),
            Item("final_exam", CalendarEventType.FinalExam, "Dönem Sonu Sınavları", 12, 105, null, SemesterType.Fall, SemesterType.Spring),
            Item("prep_final_exam", CalendarEventType.PrepSchoolExam, "Hazırlık (İngilizce) Muafiyet Sınavları", 4, 112, null, SemesterType.Fall, SemesterType.Spring),
            Item("final_publication", CalendarEventType.FinalGradePublication, "Dönem Sonu Sınav Sonuçlarının İlanı", 1, 121, null, SemesterType.Fall, SemesterType.Spring),
            Item("makeup_application", CalendarEventType.MakeupExamApplication, "Bütünleme Sınavlarının Başvurularının Son Günü (Dönem Sonu)", 1, 123, null, SemesterType.Fall, SemesterType.Spring),
            Item("makeup_exam", CalendarEventType.MakeupExam, "Bütünleme Sınavları", 5, 126, null, SemesterType.Fall, SemesterType.Spring),
            Item("makeup_publication", CalendarEventType.MakeupGradePublication, "Bütünleme Sınav Sonuçlarının İlanı İçin Son Gün", 1, 136, null, SemesterType.Fall, SemesterType.Spring),
            Item("fall_semester_break", CalendarEventType.SemesterBreak, "Yarıyıl Tatili", 22, 130, null, SemesterType.Fall)
        ];
    }

    private static IEnumerable<CalendarTemplateItemDto> DefaultAdmissionsItems()
    {
        return
        [
            Item("fall_early_payment", CalendarEventType.TuitionPayment, "Erken Ödemeler", 10, -89, null, SemesterType.Fall),
            Item("fall_international", CalendarEventType.InternationalStudentApplication, "Ön Lisans ve Lisans Programlarına Uluslararası Öğrenci Başvuru ve Kayıtları", 151, -83, null, SemesterType.Fall),
            Item("fall_transfer_app", CalendarEventType.HorizontalTransfer, "Yatay Geçiş Başvuruları", 15, -52, null, SemesterType.Fall),
            Item("fall_double_major", CalendarEventType.DoubleMajor, "Çift Anadal ve Yandal Programı Başvuruları", 15, -52, null, SemesterType.Fall),
            Item("fall_graduate_app", CalendarEventType.GraduateApplication, "Lisansüstü Programları Başvuruları", 39, -49, null, SemesterType.Fall),
            Item("fall_tuition", CalendarEventType.TuitionPayment, "Öğrenim Ücretlerinin Ödenmesi", 12, -49, null, SemesterType.Fall),
            Item("fall_transfer_eval", CalendarEventType.TransferEvaluation, "Yatay Geçiş Başvurularının Değerlendirilmesi", 15, -35, null, SemesterType.Fall),
            Item("fall_transfer_result", CalendarEventType.TransferResult, "Yatay Geçiş, Çift Anadal ve Yandal Sonuçlarının İlanı", 1, -17, null, SemesterType.Fall),
            Item("fall_transfer_registration_main", CalendarEventType.TransferRegistration, "Yatay Geçiş Kabul Edilen Öğrencilerin Kayıtları (Asil)", 3, -14, null, SemesterType.Fall),
            Item("fall_transfer_registration_wait", CalendarEventType.TransferRegistration, "Yatay Geçiş Kabul Edilen Öğrencilerin Kayıtları (Yedek)", 2, -11, null, SemesterType.Fall),
            Item("fall_prep_exam", CalendarEventType.PrepSchoolExam, "Hazırlık (İngilizce) Muafiyet Sınavları", 4, -13, null, SemesterType.Fall),
            Item("fall_exemption_result", CalendarEventType.CourseExemptionResult, "Ders Muafiyet Sonuçlarının İlan Edilmesi", 1, -10, null, SemesterType.Fall),
            Item("fall_graduate_result", CalendarEventType.GraduateResult, "Lisansüstü Program Başvuru Sonuçlarının İlanı", 1, -10, null, SemesterType.Fall),
            Item("fall_orientation", CalendarEventType.Orientation, "Yeni Kayıt Olan Öğrenciler için Oryantasyon Eğitimi", 1, -7, null, SemesterType.Fall),
            Item("fall_course_registration", CalendarEventType.CourseRegistration, "Ders Kayıt ve Danışman Onayları", 7, -7, null, SemesterType.Fall),
            Item("fall_graduate_registration", CalendarEventType.GraduateRegistration, "Lisansüstü Programları Ücret Ödeme, Ders Kayıtları ve Danışman Onayları", 5, -7, null, SemesterType.Fall),
            Item("fall_scholarship_app", CalendarEventType.ScholarshipApplication, "Burs Başvurularının Alınması", 12, 7, null, SemesterType.Fall),
            Item("fall_scholarship_result", CalendarEventType.ScholarshipResult, "Burs Başvuru Sonuçlarının Açıklanması", 1, 46, null, SemesterType.Fall),
            Item("fall_scholarship_refund", CalendarEventType.ScholarshipRefund, "Burs İadelerinin Yapılması", 1, 67, null, SemesterType.Fall),

            Item("spring_graduate_app", CalendarEventType.GraduateApplication, "Lisansüstü Programları Başvuruları", 81, -91, null, SemesterType.Spring),
            Item("spring_transfer_app", CalendarEventType.HorizontalTransfer, "Yatay Geçiş Başvuruları", 5, -28, null, SemesterType.Spring),
            Item("spring_double_major", CalendarEventType.DoubleMajor, "Çift Anadal ve Yandal Programı Başvuruları", 5, -28, null, SemesterType.Spring),
            Item("spring_tuition", CalendarEventType.TuitionPayment, "Öğrenim Ücretlerinin Ödenmesi", 19, -28, null, SemesterType.Spring),
            Item("spring_transfer_result", CalendarEventType.TransferResult, "Yatay Geçiş, Çift Anadal ve Yandal Sonuçlarının İlan Edilmesi", 1, -17, null, SemesterType.Spring),
            Item("spring_transfer_registration", CalendarEventType.TransferRegistration, "Yatay Geçiş Kabul Edilen Öğrencilerin Kayıtları (Asil-Yedek)", 5, -14, null, SemesterType.Spring),
            Item("spring_graduate_result", CalendarEventType.GraduateResult, "Lisansüstü Program Başvuru Sonuçlarının İlanı", 1, -10, null, SemesterType.Spring),
            Item("spring_course_registration", CalendarEventType.CourseRegistration, "Ders Kayıt ve Danışman Onayları", 7, -7, null, SemesterType.Spring),
            Item("spring_graduate_registration", CalendarEventType.GraduateRegistration, "Lisansüstü Programları Ücret Ödeme, Ders Kayıtları ve Danışman Onayları", 5, -7, null, SemesterType.Spring)
        ];
    }

    private static IEnumerable<CalendarTemplateItemDto> DefaultSummerItems()
    {
        return
        [
            Item("summer_registration", CalendarEventType.SummerSchoolRegistration, "Yaz Öğretimi Ücret Ödemeleri ve Ders Kayıtları", 4, -13, null, SemesterType.Summer),
            Item("summer_classes_start", CalendarEventType.ClassesStart, "Yaz Öğretimi Derslerin Başlaması", 1, 0, null, SemesterType.Summer),
            Item("summer_classes_end", CalendarEventType.ClassesEnd, "Yaz Öğretimi Derslerin Sona Ermesi", 1, 46, null, SemesterType.Summer),
            Item("summer_exam", CalendarEventType.SummerSchoolExam, "Yaz Öğretimi Sınavları", 5, 49, null, SemesterType.Summer),
            Item("summer_excuse_application", CalendarEventType.ExcuseExamApplication, "Mazeret Sınav Başvurularının Son Günü (Yaz Öğretimi)", 1, 56, null, SemesterType.Summer),
            Item("summer_excuse_exam", CalendarEventType.ExcuseExam, "Mazeret Sınavları (Yaz Öğretimi)", 1, 58, null, SemesterType.Summer),
            Item("summer_publication", CalendarEventType.SummerSchoolGradePublication, "Yaz Öğretimi Sınav Sonuçlarının İlanının Son Günü", 1, 60, null, SemesterType.Summer)
        ];
    }

    private static CalendarTemplateItemDto Item(
        string key,
        CalendarEventType eventType,
        string title,
        int duration,
        int offsetDays,
        string? anchorKey,
        params SemesterType[] semesters)
    {
        return new CalendarTemplateItemDto(
            key,
            eventType,
            title,
            duration,
            CalendarAnchorType.FromTermStart,
            anchorKey,
            offsetDays,
            semesters,
            true,
            SortOrderFromOffset(offsetDays, key),
            true);
    }

    private static int SortOrderFromOffset(int offsetDays, string key)
    {
        var keySeed = Math.Abs(key.GetHashCode(StringComparison.OrdinalIgnoreCase) % 100);
        return (offsetDays + 400) * 100 + keySeed;
    }

    private static IEnumerable<HolidayDefinition> DefaultHolidayDefinitions(DateTime now)
    {
        return
        [
            Fixed("Yılbaşı Tatili", "New Year's Day", 1, 1, 1m, now),
            Fixed("Ulusal Egemenlik ve Çocuk Bayramı", "National Sovereignty and Children's Day", 4, 23, 1m, now),
            Fixed("Emek ve Dayanışma Günü", "Labour and Solidarity Day", 5, 1, 1m, now),
            Fixed("Atatürk'ü Anma, Gençlik ve Spor Bayramı", "Commemoration of Ataturk, Youth and Sports Day", 5, 19, 1m, now),
            Fixed("Demokrasi ve Milli Birlik Günü", "Democracy and National Unity Day", 7, 15, 1m, now),
            Fixed("Zafer Bayramı", "Victory Day", 8, 30, 1m, now),
            Fixed("Cumhuriyet Bayramı", "Republic Day", 10, 28, 1.5m, now, isHalfDayStart: true, endDay: 29),
            Hijri(
                "Ramazan Bayramı",
                "Eid al-Fitr",
                10,
                1,
                3.5m,
                now,
                [
                    new HolidayOverrideDto(2025, Utc(2025, 3, 29), Utc(2025, 4, 1), false, "Diyanet"),
                    new HolidayOverrideDto(2026, Utc(2026, 3, 19), Utc(2026, 3, 22), false, "Diyanet")
                ]),
            Hijri(
                "Kurban Bayramı",
                "Eid al-Adha",
                12,
                10,
                4.5m,
                now,
                [
                    new HolidayOverrideDto(2025, Utc(2025, 6, 5), Utc(2025, 6, 9), false, "Diyanet"),
                    new HolidayOverrideDto(2026, Utc(2026, 5, 26), Utc(2026, 5, 30), false, "Diyanet")
                ])
        ];
    }

    private static HolidayDefinition Fixed(string name, string englishName, int month, int day, decimal duration, DateTime now, bool isHalfDayStart = false, int? endDay = null) =>
        new()
        {
            Name = name,
            EnglishName = englishName,
            Type = HolidayType.Fixed,
            FixedMonth = month,
            FixedDay = day,
            DurationDays = duration,
            IsHalfDayStart = isHalfDayStart,
            IsOfficialHoliday = true,
            BlocksAcademicEvents = true,
            Source = "2429",
            OverridesJson = endDay.HasValue ? "{}" : "{}",
            IsActive = true,
            CreatedAt = now
        };

    private static HolidayDefinition Important(string name, string englishName, int month, int day, DateTime now) =>
        new()
        {
            Name = name,
            EnglishName = englishName,
            Type = HolidayType.ImportantDay,
            FixedMonth = month,
            FixedDay = day,
            DurationDays = 1m,
            IsHalfDayStart = false,
            IsOfficialHoliday = false,
            BlocksAcademicEvents = true,
            Source = "UniversityPolicy",
            OverridesJson = "{}",
            IsActive = true,
            CreatedAt = now
        };

    private static HolidayDefinition Hijri(string name, string englishName, int month, int day, decimal duration, DateTime now, IReadOnlyList<HolidayOverrideDto> overrides) =>
        new()
        {
            Name = name,
            EnglishName = englishName,
            Type = HolidayType.HijriCalculated,
            HijriMonth = month,
            HijriDay = day,
            DurationDays = duration,
            IsHalfDayStart = true,
            IsOfficialHoliday = true,
            BlocksAcademicEvents = true,
            Source = "Diyanet",
            OverridesJson = SerializeOverrides(overrides),
            IsActive = true,
            CreatedAt = now
        };

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
