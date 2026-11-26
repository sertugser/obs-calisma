using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Time;

namespace SIS.Infrastructure.Services;

public class WebModuleControlService : IWebModuleControlService
{
    private static readonly TimeZoneInfo TurkeyTimeZone = ResolveTurkeyTimeZone();

    private static readonly IReadOnlyDictionary<string, WebModuleControlDefinition> Definitions
        = BuildDefinitions();

    private readonly AppDbContext _context;
    private readonly IWebModuleSettingsProvider _settingsProvider;
    private readonly IDateTimeProvider _clock;
    private readonly IInstitutionTimeZoneProvider _institutionTimeZone;

    public WebModuleControlService(
        AppDbContext context,
        IWebModuleSettingsProvider settingsProvider,
        IDateTimeProvider clock,
        IInstitutionTimeZoneProvider? institutionTimeZone = null)
    {
        _context = context;
        _settingsProvider = settingsProvider;
        _clock = clock;
        _institutionTimeZone = institutionTimeZone ?? new InstitutionTimeZoneProvider(new NullSystemConfigProvider());
    }

    // Kurum saat dilimi yalnız çekilme takvimi modunda okunur; geçersiz ayar öteki modülleri durdurmaz.
    private Func<AcademicTerm, bool> WithdrawalOpenIn(TimeZoneInfo institutionTimeZone) =>
        t => t.IsWithdrawalOpen(_clock.UtcNow, institutionTimeZone);

    public WebModuleControlDefinition? GetDefinition(string code)
        => Definitions.GetValueOrDefault(code);

    public async Task<IReadOnlyList<WebModuleControlSummary>> GetSummariesAsync(CancellationToken ct = default)
    {
        var definitions = Definitions.Values.ToList();
        var moduleKeys = definitions.Select(definition => definition.SettingModuleKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var settingKeys = definitions
            .SelectMany(definition => new[] { definition.ModeKey, definition.AnnouncementKey })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var settings = await _context.WebModuleSettings
            .AsNoTracking()
            .Where(setting => moduleKeys.Contains(setting.ModuleKey)
                && settingKeys.Contains(setting.Key))
            .ToListAsync(ct);

        var settingsByCompositeKey = settings.ToDictionary(
            setting => GetCompositeKey(setting.ModuleKey, setting.Key),
            StringComparer.OrdinalIgnoreCase);

        var activeTerm = await LoadActiveTermAsync(ct);
        var summaries = new List<WebModuleControlSummary>(definitions.Count);

        foreach (var definition in definitions)
        {
            settingsByCompositeKey.TryGetValue(GetCompositeKey(definition.SettingModuleKey, definition.ModeKey), out var modeSetting);
            settingsByCompositeKey.TryGetValue(GetCompositeKey(definition.SettingModuleKey, definition.AnnouncementKey), out var announcementSetting);

            var selectedMode = NormalizeMode(definition, modeSetting?.Value);
            var access = await EvaluateDefinitionAsync(
                definition,
                selectedMode,
                announcementSetting?.Value,
                activeTerm,
                ct);

            summaries.Add(new WebModuleControlSummary(
                definition.Code,
                definition.Title,
                definition.Category,
                definition.Description,
                definition.DisplayOrder,
                modeSetting?.Id,
                announcementSetting?.Id,
                selectedMode,
                access.SelectedModeLabel,
                access.IsOpen,
                access.EffectiveStateLabel,
                access.Message,
                announcementSetting?.Value,
                definition.Options));
        }

        return summaries;
    }

    public async Task<WebModuleAccessResult> EvaluateAsync(string code, int? termId = null, CancellationToken ct = default)
    {
        if (!Definitions.TryGetValue(code, out var definition))
            throw new InvalidOperationException($"Bilinmeyen web modül kontrolü: {code}");

        var selectedMode = NormalizeMode(
            definition,
            await _settingsProvider.GetStringAsync(definition.SettingModuleKey, definition.ModeKey, ResolveFallbackMode(definition), ct));
        var announcement = await _settingsProvider.GetStringAsync(definition.SettingModuleKey, definition.AnnouncementKey, string.Empty, ct);

        AcademicTerm? term = termId.HasValue
            ? await LoadTermAsync(termId.Value, ct)
            : await LoadActiveTermAsync(ct);

        return await EvaluateDefinitionAsync(definition, selectedMode, announcement, term, ct);
    }

    private async Task<WebModuleAccessResult> EvaluateDefinitionAsync(
        WebModuleControlDefinition definition,
        string selectedMode,
        string? announcement,
        AcademicTerm? term,
        CancellationToken ct)
    {
        var selectedOption = definition.Options.FirstOrDefault(option =>
            string.Equals(option.Code, selectedMode, StringComparison.OrdinalIgnoreCase))
            ?? definition.Options[0];

        WebModuleAccessResult result = selectedMode switch
        {
            WebModuleControlModes.Open => new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: true,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Açık",
                Message: null),

            WebModuleControlModes.Closed => new WebModuleAccessResult(
                IsEnabled: false,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Kapalı",
                Message: $"{definition.Title} modülü kapatılmıştır."),

            WebModuleControlModes.TermEventBased => await EvaluateTermEventBasedAccessAsync(
                definition,
                term,
                selectedOption,
                announcement,
                ct),

            WebModuleControlModes.AcademicCalendarRegistration => EvaluateCalendarBoundAccess(
                term,
                selectedOption,
                announcement,
                definition.Title,
                t => t.IsRegistrationOpen(_clock.UtcNow),
                "Aktif akademik dönem bulunamadı.",
                "Aktif dönemin akademik takvimi tanımlı olmadığı için ders kayıt modülü açılamıyor.",
                "Ders kayıt süreci şu anda akademik takvimde açık değil."),

            WebModuleControlModes.AcademicCalendarAddDrop => EvaluateCalendarBoundAccess(
                term,
                selectedOption,
                announcement,
                definition.Title,
                t => t.IsAddDropOpen(_clock.UtcNow),
                "Aktif akademik dönem bulunamadı.",
                "Aktif dönemin akademik takvimi tanımlı olmadığı için ders ekle/bırak modülü açılamıyor.",
                "Ders ekle/bırak süreci şu anda akademik takvimde açık değil."),

            WebModuleControlModes.AcademicCalendarWithdrawal => EvaluateCalendarBoundAccess(
                term,
                selectedOption,
                announcement,
                definition.Title,
                WithdrawalOpenIn(await _institutionTimeZone.GetAsync(ct)),
                "Aktif akademik dönem bulunamadı.",
                "Aktif dönemin akademik takvimi tanımlı olmadığı için dersten çekilme modülü açılamıyor.",
                "Dersten çekilme süreci şu anda akademik takvimde açık değil."),

            WebModuleControlModes.GradeEntryPeriod => EvaluateCalendarBoundAccess(
                term,
                selectedOption,
                announcement,
                definition.Title,
                t => t.IsGradeEntryOpen(_clock.UtcNow),
                "Aktif akademik dönem bulunamadı.",
                "Aktif dönemin akademik takvimi tanımlı olmadığı için not girişi modülü açılamıyor.",
                "Not girişi süreci şu anda not giriş süre tanımlarında açık değil."),

            // Bilinmeyen/tanımsız mod → fail-closed (whitelist): açık değil.
            _ => new WebModuleAccessResult(
                IsEnabled: false,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Kapalı",
                Message: $"{definition.Title} modülü için geçerli bir kontrol modu tanımlı değil."),
        };

        if (selectedMode == WebModuleControlModes.TermEventBased
            && definition.Code == "student-course-registration"
            && result.IsOpen)
            result = await ApplyDailyRegistrationOpeningAsync(result, announcement, ct);

        if (selectedMode == WebModuleControlModes.TermEventBased
            && definition.Code == "student-new-course-registration"
            && result.IsOpen)
            result = await ApplyDailyRegistrationOpeningAsync(result, announcement, ct);

        if (selectedMode == WebModuleControlModes.TermEventBased
            && definition.Code == "student-extended-course-registration"
            && result.IsOpen)
            result = await ApplyDailyRegistrationOpeningAsync(result, announcement, ct);

        return result;
    }

    private Task<WebModuleAccessResult> EvaluateTermEventBasedAccessAsync(
        WebModuleControlDefinition definition,
        AcademicTerm? term,
        WebModuleControlOption selectedOption,
        string? announcement,
        CancellationToken ct)
    {
        if (term is null)
        {
            return Task.FromResult(new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Beklemede",
                Message: ResolveMessage(announcement, "Aktif akademik dönem bulunamadı.")));
        }

        var now = _clock.UtcNow;
        var access = definition.Code switch
        {
            "student-course-registration" => EvaluateActiveTermEvent(term, CalendarEventType.CourseRegistration, selectedOption, announcement, "Ders kayıt penceresi şu anda açık değil.", now),
            "student-new-course-registration" => EvaluateActiveTermEvent(term, CalendarEventType.NewStudentRegistration, selectedOption, announcement, "Yeni öğrenci ders kayıt penceresi şu anda açık değil.", now),
            "student-extended-course-registration" => EvaluateActiveTermEvent(term, CalendarEventType.CourseRegistration, selectedOption, announcement, "Öğrenim süresini aşan öğrenciler için ders kayıt penceresi şu anda açık değil.", now),
            "advisor-registration-approval" => EvaluateActiveTermEvents(term, new[] { CalendarEventType.AdvisorApproval, CalendarEventType.CourseRegistration }, selectedOption, announcement, "Danışman onayı penceresi şu anda açık değil.", now),
            "advisor-new-student-registration-approval" => EvaluateActiveTermEvents(term, new[] { CalendarEventType.AdvisorApproval, CalendarEventType.NewStudentRegistration, CalendarEventType.CourseRegistration }, selectedOption, announcement, "Yeni öğrenci danışman onayı penceresi şu anda açık değil.", now),
            "student-add-drop" => EvaluateActiveTermEvent(term, CalendarEventType.AddDrop, selectedOption, announcement, "Ders ekle/bırak penceresi şu anda açık değil.", now),
            "student-new-add-drop" => EvaluateActiveTermEvent(term, CalendarEventType.AddDrop, selectedOption, announcement, "Yeni öğrenci ders ekle/bırak penceresi şu anda açık değil.", now),
            "advisor-add-drop-approval" => EvaluateActiveTermEvents(term, new[] { CalendarEventType.AddDropApproval, CalendarEventType.AddDrop }, selectedOption, announcement, "Ders ekle/bırak onay penceresi şu anda açık değil.", now),
            "student-withdrawal" => EvaluateActiveTermEvent(term, CalendarEventType.CourseWithdrawal, selectedOption, announcement, "Dersten çekilme penceresi şu anda açık değil.", now),
            "midterm-grade-entry" => EvaluateActiveTermEvent(term, CalendarEventType.MidtermGradeEntry, selectedOption, announcement, "Ara sınav not girişi penceresi şu anda açık değil.", now),
            "midterm-excuse-grade-entry" => EvaluateActiveTermEvent(term, CalendarEventType.ExcuseExam, selectedOption, announcement, "Ara sınav mazeret not girişi penceresi şu anda açık değil.", now, "midterm_excuse_exam"),
            "final-grade-entry" => EvaluateActiveTermEvent(term, CalendarEventType.FinalGradeEntry, selectedOption, announcement, "Yarıyıl sınav not girişi penceresi şu anda açık değil.", now),
            "makeup-grade-entry" => EvaluateActiveTermEvent(term, CalendarEventType.MakeupGradeEntry, selectedOption, announcement, "Bütünleme not girişi penceresi şu anda açık değil.", now),
            "midterm-grade-publication" => EvaluatePublicationEvent(term, CalendarEventType.MidtermGradePublication, selectedOption, announcement, "Ara sınav notları henüz öğrenciye yayınlanamaz.", now),
            "midterm-excuse-grade-publication" => EvaluatePublicationEvent(term, CalendarEventType.ExcuseExamResult, selectedOption, announcement, "Ara sınav mazeret notları henüz öğrenciye yayınlanamaz.", now),
            "final-grade-publication" => EvaluatePublicationEvent(term, CalendarEventType.FinalGradePublication, selectedOption, announcement, "Yarıyıl sınav notları henüz öğrenciye yayınlanamaz.", now),
            "makeup-grade-publication" => EvaluatePublicationEvent(term, CalendarEventType.MakeupGradePublication, selectedOption, announcement, "Bütünleme notları henüz öğrenciye yayınlanamaz.", now),
            "letter-grade-publication" => EvaluatePublicationEvent(term, CalendarEventType.FinalGradePublication, selectedOption, announcement, "Harf notları henüz öğrenciye yayınlanamaz.", now),
            "makeup-letter-grade-publication" => EvaluatePublicationEvent(term, CalendarEventType.MakeupGradePublication, selectedOption, announcement, "Bütünleme harf notları henüz öğrenciye yayınlanamaz.", now),
            "social-transcript-application-evaluation" => EvaluatePublicationEvent(term, CalendarEventType.FinalGradePublication, selectedOption, announcement, "Sosyal transkript değerlendirme akışı şu anda açık değil.", now),
            "social-transcript-student-objection" => EvaluatePublicationEvent(term, CalendarEventType.FinalGradePublication, selectedOption, announcement, "Sosyal transkript itiraz akışı şu anda açık değil.", now),
            "registration-freeze-application" => EvaluateDeadlineWindow(term, CalendarEventType.RegistrationFreezeDeadline, selectedOption, announcement, "Kayıt dondurma başvuru penceresi şu anda açık değil.", now),
            // Takvim olayına eşlenmemiş tanım → fail-closed: açık değil.
            _ => new WebModuleAccessResult(
                IsEnabled: false,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Kapalı",
                Message: ResolveMessage(announcement, $"{definition.Title} için takvim olayı tanımlı değil.")),
        };

        return Task.FromResult(access);
    }

    private WebModuleAccessResult EvaluateActiveTermEvent(
        AcademicTerm term,
        CalendarEventType eventType,
        WebModuleControlOption selectedOption,
        string? announcement,
        string closedMessage,
        DateTime now,
        string? templateItemKey = null)
        => EvaluateActiveTermEvents(term, new[] { eventType }, selectedOption, announcement, closedMessage, now, templateItemKey);

    private WebModuleAccessResult EvaluateActiveTermEvents(
        AcademicTerm term,
        IReadOnlyCollection<CalendarEventType> eventTypes,
        WebModuleControlOption selectedOption,
        string? announcement,
        string closedMessage,
        DateTime now,
        string? templateItemKey = null)
    {
        var calendarEvents = term.CalendarEvents
            .Where(evt => eventTypes.Contains(evt.EventType))
            .Where(evt => templateItemKey == null || string.Equals(evt.TemplateItemKey, templateItemKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(evt => evt.StartDate)
            .ToList();

        if (!calendarEvents.Any())
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Takvim Eksik",
                Message: ResolveMessage(announcement, $"{string.Join(", ", eventTypes)} etkinliği takvimde tanımlı değil."));
        }

        var activeEvent = calendarEvents.FirstOrDefault(calendarEvent =>
        {
            var eventEnd = calendarEvent.EndDate ?? calendarEvent.StartDate;
            return now >= calendarEvent.StartDate && now <= eventEnd;
        });

        if (activeEvent is null)
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Takvime Bağlı",
                Message: ResolveMessage(announcement, closedMessage));
        }

        return new WebModuleAccessResult(
            IsEnabled: true,
            IsOpen: true,
            SelectedMode: selectedOption.Code,
            SelectedModeLabel: selectedOption.Label,
            EffectiveStateLabel: "Açık",
            Message: null);
    }

    private WebModuleAccessResult EvaluatePublicationEvent(
        AcademicTerm term,
        CalendarEventType eventType,
        WebModuleControlOption selectedOption,
        string? announcement,
        string closedMessage,
        DateTime now)
    {
        var calendarEvent = term.CalendarEvents
            .Where(evt => evt.EventType == eventType)
            .OrderBy(evt => evt.StartDate)
            .LastOrDefault();

        if (calendarEvent is null)
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Takvim Eksik",
                Message: ResolveMessage(announcement, $"{eventType} etkinliği takvimde tanımlı değil."));
        }

        if (now < calendarEvent.StartDate)
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Takvime Bağlı",
                Message: ResolveMessage(announcement, closedMessage));
        }

        return new WebModuleAccessResult(
            IsEnabled: true,
            IsOpen: true,
            SelectedMode: selectedOption.Code,
            SelectedModeLabel: selectedOption.Label,
            EffectiveStateLabel: "Açık",
            Message: null);
    }

    private WebModuleAccessResult EvaluateDeadlineWindow(
        AcademicTerm term,
        CalendarEventType eventType,
        WebModuleControlOption selectedOption,
        string? announcement,
        string closedMessage,
        DateTime now)
    {
        var calendarEvent = term.CalendarEvents
            .Where(evt => evt.EventType == eventType)
            .OrderBy(evt => evt.StartDate)
            .LastOrDefault();

        if (calendarEvent is null)
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Takvim Eksik",
                Message: ResolveMessage(announcement, $"{eventType} etkinliği takvimde tanımlı değil."));
        }

        if (now < term.TermStart || now > calendarEvent.StartDate)
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Takvime Bağlı",
                Message: ResolveMessage(announcement, closedMessage));
        }

        return new WebModuleAccessResult(
            IsEnabled: true,
            IsOpen: true,
            SelectedMode: selectedOption.Code,
            SelectedModeLabel: selectedOption.Label,
            EffectiveStateLabel: "Açık",
            Message: null);
    }

    private WebModuleAccessResult EvaluateCalendarBoundAccess(
        AcademicTerm? term,
        WebModuleControlOption selectedOption,
        string? announcement,
        string title,
        Func<AcademicTerm, bool> isOpen,
        string termMissingMessage,
        string calendarMissingMessage,
        string closedMessage)
    {
        if (term is null)
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Beklemede",
                Message: ResolveMessage(announcement, termMissingMessage));
        }

        if (term.AcademicCalendar is null)
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Takvim Eksik",
                Message: ResolveMessage(announcement, calendarMissingMessage));
        }

        if (!isOpen(term))
        {
            return new WebModuleAccessResult(
                IsEnabled: true,
                IsOpen: false,
                SelectedMode: selectedOption.Code,
                SelectedModeLabel: selectedOption.Label,
                EffectiveStateLabel: "Takvime Bağlı",
                Message: ResolveMessage(announcement, closedMessage));
        }

        return new WebModuleAccessResult(
            IsEnabled: true,
            IsOpen: true,
            SelectedMode: selectedOption.Code,
            SelectedModeLabel: selectedOption.Label,
            EffectiveStateLabel: "Açık",
            Message: null);
    }

    private async Task<WebModuleAccessResult> ApplyDailyRegistrationOpeningAsync(
        WebModuleAccessResult current,
        string? announcement,
        CancellationToken ct)
    {
        var openAt = await _settingsProvider.GetTimeAsync("StudentPanel", "CourseRegistrationOpenAt", TimeOnly.MinValue, ct);
        if (openAt <= TimeOnly.MinValue)
            return current;

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TurkeyTimeZone);
        if (TimeOnly.FromDateTime(localNow) >= openAt)
            return current;

        return current with
        {
            IsOpen = false,
            EffectiveStateLabel = "Saat Bekliyor",
            Message = ResolveMessage(
                announcement,
                $"Ders kayıt işlemleri bugün saat {openAt.ToString("HH:mm", CultureInfo.InvariantCulture)} itibarıyla açılacaktır."),
        };
    }

    private async Task<AcademicTerm?> LoadActiveTermAsync(CancellationToken ct)
    {
        // Tek kaynak: AcademicTerms.ActiveTerm() extension (CLAUDE.md değişmezi).
        return await _context.AcademicTerms
            .AsNoTracking()
            .Include(term => term.AcademicCalendar)
            .Include(term => term.CalendarEvents)
            .ActiveTerm()
            .FirstOrDefaultAsync(ct);
    }

    private async Task<AcademicTerm?> LoadTermAsync(int termId, CancellationToken ct)
    {
        return await _context.AcademicTerms
            .AsNoTracking()
            .Include(term => term.AcademicCalendar)
            .Include(term => term.CalendarEvents)
            .FirstOrDefaultAsync(term => term.Id == termId, ct);
    }

    private static string NormalizeMode(WebModuleControlDefinition definition, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var match = definition.Options.FirstOrDefault(option =>
                string.Equals(option.Code, value.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Code;
        }

        return ResolveFallbackMode(definition);
    }

    /// <summary>
    /// Ayar eksik veya süresi dolmuşsa kullanılacak GÜVENLİ varsayılan mod.
    /// Asla "Açık"a fail-open olmaz: takvime/olaya bağlı mod varsa onu, yoksa "Kapalı" döner.
    /// (Karar: eksik/süresi dolmuş kayıt modülü → akademik takvime bağlı davranır.)
    /// </summary>
    private static string ResolveFallbackMode(WebModuleControlDefinition definition)
    {
        var calendarBound = definition.Options.FirstOrDefault(option =>
            option.Code is WebModuleControlModes.AcademicCalendarRegistration
                or WebModuleControlModes.AcademicCalendarAddDrop
                or WebModuleControlModes.AcademicCalendarWithdrawal
                or WebModuleControlModes.GradeEntryPeriod
                or WebModuleControlModes.TermEventBased);
        if (calendarBound is not null)
            return calendarBound.Code;

        var closed = definition.Options.FirstOrDefault(option =>
            option.Code == WebModuleControlModes.Closed);
        return closed?.Code ?? definition.Options[0].Code;
    }

    private static string? ResolveMessage(string? preferred, string fallback)
        => string.IsNullOrWhiteSpace(preferred) ? fallback : preferred.Trim();

    private static string GetCompositeKey(string moduleKey, string key) => $"{moduleKey}:{key}";

    private static IReadOnlyDictionary<string, WebModuleControlDefinition> BuildDefinitions()
    {
        var registrationOptions = new[]
        {
            new WebModuleControlOption(WebModuleControlModes.Open, "Açık", "Takvim penceresinden bağımsız olarak modülü manuel açar."),
            new WebModuleControlOption(WebModuleControlModes.Closed, "Kapalı", "Modülü tamamen kapatır."),
            new WebModuleControlOption(WebModuleControlModes.AcademicCalendarRegistration, "Ak.Takvime Bağlı", "Dönemin kayıt penceresi açıksa modülü açar."),
        };

        var addDropOptions = new[]
        {
            new WebModuleControlOption(WebModuleControlModes.Open, "Açık", "Takvim penceresinden bağımsız olarak modülü manuel açar."),
            new WebModuleControlOption(WebModuleControlModes.Closed, "Kapalı", "Modülü tamamen kapatır."),
            new WebModuleControlOption(WebModuleControlModes.AcademicCalendarAddDrop, "Ak.Takvime Bağlı", "Dönemin ekle/bırak penceresi açıksa modülü açar."),
        };

        var withdrawalOptions = new[]
        {
            new WebModuleControlOption(WebModuleControlModes.Open, "Açık", "Takvim penceresinden bağımsız olarak modülü manuel açar."),
            new WebModuleControlOption(WebModuleControlModes.Closed, "Kapalı", "Modülü tamamen kapatır."),
            new WebModuleControlOption(WebModuleControlModes.AcademicCalendarWithdrawal, "Ak.Takvime Bağlı", "Dönemin çekilme penceresi açıksa modülü açar."),
        };

        var gradeEntryOptions = new[]
        {
            new WebModuleControlOption(WebModuleControlModes.Open, "Açık", "Not girişini manuel olarak açar."),
            new WebModuleControlOption(WebModuleControlModes.Closed, "Kapalı", "Not girişini tamamen kapatır."),
            new WebModuleControlOption(WebModuleControlModes.GradeEntryPeriod, "Not Giriş Süre Tanımlarına Bağlı", "Dönemin not giriş penceresi açıksa modülü açar."),
        };

        var eventDrivenOptions = new[]
        {
            new WebModuleControlOption(WebModuleControlModes.Open, "Açık", "Takvim olayından bağımsız olarak modülü manuel açar."),
            new WebModuleControlOption(WebModuleControlModes.Closed, "Kapalı", "Modülü tamamen kapatır."),
            new WebModuleControlOption(WebModuleControlModes.TermEventBased, "Ak.Takvime Bağlı", "İlgili akademik takvim etkinliği aktif olduğunda modülü açar."),
        };

        var binaryOptions = new[]
        {
            new WebModuleControlOption(WebModuleControlModes.Open, "Açık", "Özelliği aktif eder."),
            new WebModuleControlOption(WebModuleControlModes.Closed, "Kapalı", "Özelliği kapatır."),
        };

        var definitions = new[]
        {
            new WebModuleControlDefinition(
                "student-new-course-registration",
                "Yeni Öğrenci Ders Kayıt",
                "Öğrenci İşlemleri",
                "Yeni öğrenci kayıt akışının ayrı takvim penceresini yönetir.",
                10,
                "NewStudentCourseRegistration",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "student-extended-course-registration",
                "Öğrenim Süresini Aşan Öğrenci Ders Kayıt",
                "Öğrenci İşlemleri",
                "Normal öğrenim süresini aşan öğrencilerin kayıt akışını ayrı modül kararıyla yönetir.",
                30,
                "ExtendedStudentCourseRegistration",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "student-course-registration",
                "Ders Kayıt",
                "Öğrenci İşlemleri",
                "Öğrencinin kayıt planı oluşturma ve ders seçme akışını yönetir.",
                40,
                "StudentCourseRegistration",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "advisor-new-student-registration-approval",
                "Yeni Öğrenci Danışman Onay",
                "Öğrenci İşlemleri",
                "Yeni öğrencilerin danışman kayıt onay akışını ayrı modül kararıyla yönetir.",
                45,
                "AdvisorNewStudentRegistrationApproval",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "student-add-drop",
                "Ders Ekle/Bırak",
                "Öğrenci İşlemleri",
                "Öğrencinin mevcut planındaki dersleri ekleme veya bırakma akışını yönetir.",
                70,
                "StudentAddDrop",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "student-new-add-drop",
                "Yeni Öğrenci Ders Ekle/Bırak",
                "Öğrenci İşlemleri",
                "Yeni öğrencinin mevcut planındaki dersleri ekleme veya bırakma akışını ayrı modül kararıyla yönetir.",
                65,
                "NewStudentAddDrop",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "student-withdrawal",
                "Dersten Çekilme",
                "Öğrenci İşlemleri",
                "Öğrencinin dersten çekilme talebi oluşturma akışını yönetir.",
                90,
                "StudentWithdrawal",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "advisor-registration-approval",
                "Danışman Onay",
                "Öğrenci İşlemleri",
                "Kayıt dönemindeki danışman onay akışını yönetir.",
                50,
                "AdvisorRegistrationApproval",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "advisor-add-drop-approval",
                "Ders Ekle/Bırak Onay",
                "Öğrenci İşlemleri",
                "Ekle/bırak dönemindeki danışman onay akışını yönetir.",
                80,
                "AdvisorAddDropApproval",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "instructor-grade-entry",
                "Öğretim Elemanı Not Girişi",
                "Öğretim Elemanı",
                "Bileşen bazlı not giriş ve sonlandırma akışını yönetir.",
                200,
                "InstructorGradeEntry",
                "Mode",
                "Announcement",
                gradeEntryOptions),

            new WebModuleControlDefinition(
                "midterm-grade-entry",
                "Ara Sınav Not Giriş",
                "Not Yönetimi",
                "Ara sınav bileşenleri için not giriş penceresini yönetir.",
                100,
                "MidtermGradeEntry",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "midterm-grade-publication",
                "Ara Sınav Notlarının Öğrenciye Yayınlanması",
                "Not Yönetimi",
                "Ara sınav sonuçlarının öğrenci ekranına yansımasını yönetir.",
                110,
                "MidtermGradePublication",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "midterm-excuse-grade-entry",
                "Ara Sınav Mazeret Not Giriş",
                "Not Yönetimi",
                "Ara sınav mazeret bileşenleri için not giriş penceresini yönetir.",
                120,
                "MidtermExcuseGradeEntry",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "midterm-excuse-grade-publication",
                "Ara Sınav Mazeret Notlarının Öğrenciye Yayınlanması",
                "Not Yönetimi",
                "Ara sınav mazeret sonuçlarının öğrenci ekranına yansımasını yönetir.",
                130,
                "MidtermExcuseGradePublication",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "final-grade-entry",
                "Yarıyıl Sınav Not Giriş",
                "Not Yönetimi",
                "Yarıyıl/final bileşenleri için not giriş penceresini yönetir.",
                140,
                "FinalGradeEntry",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "final-grade-publication",
                "Yarıyıl Sınav Notlarının Öğrenciye Yayınlanması",
                "Not Yönetimi",
                "Yarıyıl/final sonuçlarının öğrenci ekranına yansımasını yönetir.",
                150,
                "FinalGradePublication",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "makeup-grade-entry",
                "Bütünleme (YYS Mazeret) Not Giriş",
                "Not Yönetimi",
                "Bütünleme ve YYS mazeret bileşenleri için not giriş penceresini yönetir.",
                160,
                "MakeupGradeEntry",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "makeup-grade-publication",
                "Bütünleme (YYS Mazeret) Notlarının Öğrenciye Yayınlanması",
                "Not Yönetimi",
                "Bütünleme/YYS mazeret sonuçlarının öğrenci ekranına yansımasını yönetir.",
                170,
                "MakeupGradePublication",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "letter-grade-publication",
                "Harf Notlarının Öğrenciye Yayınlanması",
                "Not Yönetimi",
                "Harf notlarının öğrenci ekranında görünmesini yönetir.",
                180,
                "LetterGradePublication",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "makeup-letter-grade-publication",
                "Bütünleme Harf Notlarının Öğrenciye Yayınlanması",
                "Not Yönetimi",
                "Bütünleme sonrası harf notlarının öğrenci ekranında görünmesini yönetir.",
                190,
                "MakeupLetterGradePublication",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "social-transcript-application-evaluation",
                "Sosyal Transkript Başvuru Değerlendirme",
                "Öğrenci İşlemleri",
                "Öğrencinin dönem/GPA bazlı transkript değerlendirme akışını yönetir.",
                240,
                "SocialTranscriptApplicationEvaluation",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "social-transcript-student-objection",
                "Sosyal Transkript Öğrenci Başvuru İtiraz",
                "Öğrenci İşlemleri",
                "Öğrencinin ders bazlı not detayları üzerinden itiraz hazırlık akışını yönetir.",
                250,
                "SocialTranscriptStudentObjection",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "registration-freeze-application",
                "Kayıt Dondurma Başvuru",
                "Öğrenci İşlemleri",
                "Aktif dönemde kayıt dondurma başvuru penceresinin açık olup olmadığını yönetir.",
                260,
                "RegistrationFreezeApplication",
                "Mode",
                "Announcement",
                eventDrivenOptions),

            new WebModuleControlDefinition(
                "instructor-student-phone",
                "Öğrenci Telefon Görünürlüğü",
                "Öğretim Elemanı",
                "Ders listelerinde öğrenci telefon bilgisinin gösterilip gösterilmeyeceğini yönetir.",
                210,
                "InstructorStudentPhone",
                "Mode",
                "Announcement",
                binaryOptions),

            new WebModuleControlDefinition(
                "announcements-feed",
                "İç Duyuru Akışı",
                "Genel",
                "Dashboard ve duyuru listelerinde aktif iç duyuruların yayınlanmasını yönetir.",
                220,
                "AnnouncementsFeed",
                "Mode",
                "Announcement",
                binaryOptions),
        };

        return definitions.ToDictionary(definition => definition.Code, StringComparer.OrdinalIgnoreCase);
    }

    private static TimeZoneInfo ResolveTurkeyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }
    }
}
