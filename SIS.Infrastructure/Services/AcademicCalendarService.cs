using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIS.Application.AcademicCalendar.DTOs;
using SIS.Application.AcademicCalendar.Services;
using SIS.Application.CalendarGenerator.DTOs;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.CalendarGenerator;
using SIS.Infrastructure.Services.Time;

namespace SIS.Infrastructure.Services;

public class AcademicCalendarService : IAcademicCalendarService
{
    private const int DependentPublicationBusinessDays = 3;
    private const int GradePublicationBusinessDayLimit = 10;
    private const int ExcuseExamMinBusinessDays = 3;
    private const int MakeupExamMinBusinessDays = 5;
    private const int MakeupAfterPublicationBufferDays = 4;
    private const int FlexibleHolidayOverlapMinDays = 15;

    private readonly AppDbContext _ctx;
    private readonly IDateTimeProvider _clock;
    private readonly IInstitutionTimeZoneProvider _institutionTimeZone;

    public AcademicCalendarService(AppDbContext ctx, IDateTimeProvider clock, IInstitutionTimeZoneProvider? institutionTimeZone = null)
    {
        _ctx   = ctx;
        _clock = clock;
        _institutionTimeZone = institutionTimeZone ?? new InstitutionTimeZoneProvider(new SIS.Application.Common.Services.NullSystemConfigProvider());
    }

    // ── Admin ─────────────────────────────────────────────────────────

    public async Task<AcademicCalendarDto?> GetByTermIdAsync(int termId, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Term {termId} not found.");

        if (term.AcademicCalendar is null)
            return null;

        return ToDto(term, term.AcademicCalendar, await _institutionTimeZone.GetAsync(ct));
    }

    public async Task<AcademicCalendarDto> UpsertAsync(int termId, UpsertAcademicCalendarDto dto, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Term {termId} not found.");

        if (term.IsCancelled)
            throw new InvalidOperationException("Bu dönem iptal edilmiş; takvim oluşturulamaz.");

        Validate(dto, term);

        var isNew = term.AcademicCalendar is null;
        var cal = term.AcademicCalendar ?? new TermAcademicCalendar
        {
            TermId    = term.Id,
            CreatedAt = _clock.UtcNow
        };

        if (!isNew) cal.UpdatedAt = _clock.UtcNow;

        cal.RegistrationStart  = DateTime.SpecifyKind(dto.RegistrationStart,  DateTimeKind.Utc);
        cal.RegistrationEnd    = DateTime.SpecifyKind(dto.RegistrationEnd,    DateTimeKind.Utc);
        cal.AddDropStart       = DateTime.SpecifyKind(dto.AddDropStart,       DateTimeKind.Utc);
        cal.AddDropEnd         = DateTime.SpecifyKind(dto.AddDropEnd,         DateTimeKind.Utc);
        cal.WithdrawalDeadline = DateTime.SpecifyKind(dto.WithdrawalDeadline, DateTimeKind.Utc);
        cal.GradeEntryStart    = DateTime.SpecifyKind(dto.GradeEntryStart,    DateTimeKind.Utc);
        cal.GradeEntryEnd      = DateTime.SpecifyKind(dto.GradeEntryEnd,      DateTimeKind.Utc);

        if (isNew)
        {
            term.AcademicCalendar = cal;
            _ctx.TermAcademicCalendars.Add(cal);
        }

        var institutionTimeZone = await _institutionTimeZone.GetAsync(ct); // bozuk ayar kayıttan önce hata verir
        await _ctx.SaveChangesAsync(ct);
        return ToDto(term, term.AcademicCalendar!, institutionTimeZone);
    }

    public async Task UpdatePhaseOverrideAsync(int termId, UpdateCalendarPhaseOverrideDto dto, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Term {termId} not found.");

        if (term.IsCancelled)
            throw new InvalidOperationException("İptal edilmiş dönemde override değiştirilemez.");

        var cal = term.AcademicCalendar
            ?? throw new InvalidOperationException("Bu dönem için henüz akademik takvim oluşturulmamış.");

        switch (dto.Phase)
        {
            case "Registration": cal.RegistrationOverride = dto.Value; break;
            case "AddDrop":      cal.AddDropOverride      = dto.Value; break;
            case "Withdrawal":   cal.WithdrawalOverride   = dto.Value; break;
            case "GradeEntry":   cal.GradeEntryOverride   = dto.Value; break;
            default:
                throw new ArgumentException($"Geçersiz faz: '{dto.Phase}'. " +
                    "Geçerli değerler: Registration, AddDrop, Withdrawal, GradeEntry.");
        }

        cal.UpdatedAt = _clock.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<StudentAcademicCalendarDto> UpdateEventAsync(int termId, int eventId, UpdateTermCalendarEventDto dto, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .Include(t => t.CalendarEvents)
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Term {termId} not found.");

        if (term.IsCancelled)
            throw new InvalidOperationException("İptal edilmiş dönemde etkinlik taşınamaz.");

        var calendarEvent = term.CalendarEvents.FirstOrDefault(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Calendar event {eventId} not found.");

        if (!calendarEvent.IsPublic)
            throw new InvalidOperationException("Sadece akademik takvimde görünür etkinlikler taşınabilir.");

        if (IsLockedCalendarEvent(calendarEvent))
            throw new InvalidOperationException("Resmi tatiller ve bloklayıcı özel günler sürükle-bırak ile taşınamaz.");

        var changedEventIds = RecalculateEventMove(term, calendarEvent.Id, dto.StartDate, dto.EndDate, dto.PropagateChanges);

        if (term.AcademicCalendar is not null)
        {
            SyncAcademicCalendarFromEvents(term);
            if (term.CalendarEvents.Any(e => changedEventIds.Contains(e.Id) && AffectsAcademicCalendarSummary(e.EventType)))
                Validate(BuildUpsertDto(term.AcademicCalendar), term);
            term.AcademicCalendar.UpdatedAt = _clock.UtcNow;
        }

        var institutionTimeZone = await _institutionTimeZone.GetAsync(ct); // bozuk ayar kayıttan önce hata verir
        await _ctx.SaveChangesAsync(ct);
        return BuildStudentDto(term, _clock.UtcNow, institutionTimeZone);
    }

    public async Task<StudentAcademicCalendarDto> PreviewEventMoveAsync(int termId, int eventId, PreviewTermCalendarEventMoveDto dto, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .Include(t => t.CalendarEvents)
            .FirstOrDefaultAsync(t => t.Id == termId, ct)
            ?? throw new KeyNotFoundException($"Term {termId} not found.");

        if (term.IsCancelled)
            throw new InvalidOperationException("İptal edilmiş dönemde etkinlik taşınamaz.");

        ApplyClientEvents(term, dto.Events);
        RecalculateEventMove(term, eventId, dto.StartDate, dto.EndDate, dto.PropagateChanges);
        if (term.AcademicCalendar is not null)
            SyncAcademicCalendarFromEvents(term);

        return BuildStudentDto(term, _clock.UtcNow, await _institutionTimeZone.GetAsync(ct));
    }

    public async Task<IReadOnlyList<StudentAcademicCalendarDto>> SaveEventChangesAsync(SaveAcademicCalendarChangesDto dto, CancellationToken ct = default)
    {
        if (dto.Calendars.Count == 0)
            throw new InvalidOperationException("Kaydedilecek takvim degisikligi bulunamadi.");

        var termIds = dto.Calendars.Select(c => c.TermId).Distinct().ToList();
        var terms = await _ctx.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .Include(t => t.CalendarEvents)
            .Where(t => termIds.Contains(t.Id))
            .ToListAsync(ct);
        if (terms.Count != termIds.Count)
            throw new KeyNotFoundException("Kaydedilecek donemlerden biri bulunamadi.");

        var institutionTimeZone = await _institutionTimeZone.GetAsync(ct);
        await using var tx = _ctx.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"
            ? null
            : await _ctx.Database.BeginTransactionAsync(ct);

        try
        {
            foreach (var stagedCalendar in dto.Calendars)
            {
                var term = terms.First(t => t.Id == stagedCalendar.TermId);
                if (term.IsCancelled)
                    throw new InvalidOperationException("İptal edilmiş dönemde takvim kaydedilemez.");

                ApplyClientEvents(term, stagedCalendar.Events);
            }

            foreach (var term in terms)
                ValidateTermReadyToSave(term);

            var now = _clock.UtcNow;
            foreach (var term in terms)
            {
                term.UpdatedAt = now;
                if (term.AcademicCalendar is not null)
                    term.AcademicCalendar.UpdatedAt = now;
            }

            await _ctx.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);

            return terms.OrderBy(t => t.TermStart).Select(t => BuildStudentDto(t, now, institutionTimeZone)).ToList();
        }
        catch
        {
            if (tx is not null)
                await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<AcademicCalendarTermSummaryDto>> ListTermsAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var terms = await _ctx.AcademicTerms
            .AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .OrderByDescending(t => t.Year)
            .ThenBy(t => t.TermType)
            .ToListAsync(ct);

        return terms.Select(t => new AcademicCalendarTermSummaryDto(
            TermId:       t.Id,
            TermName:     t.Name,
            Year:         t.Year,
            TermTypeLabel: TermLabels.Get(t.TermType),
            IsActive:     t.IsActive(now),
            IsCancelled:  t.IsCancelled,
            HasCalendar:  t.AcademicCalendar is not null,
            TermStart:    t.TermStart,
            TermEnd:      t.TermEnd
        )).ToList();
    }

    private static HashSet<int> RecalculateEventMove(AcademicTerm term, int eventId, DateTime requestedStartDate, DateTime? requestedEndDate, bool? propagateChanges)
    {
        var calendarEvent = term.CalendarEvents.FirstOrDefault(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Calendar event {eventId} not found.");

        if (!calendarEvent.IsPublic)
            throw new InvalidOperationException("Sadece akademik takvimde gorunur etkinlikler tasinabilir.");

        if (IsLockedCalendarEvent(calendarEvent))
            throw new InvalidOperationException("Resmi tatiller ve bloklayici ozel gunler surukle-birak ile tasinamaz.");

        var startDate = EnsureUtcDate(requestedStartDate);
        var endDate = EnsureUtcDate(requestedEndDate ?? requestedStartDate);
        endDate = TrimTrailingWeekendEnd(calendarEvent, startDate, endDate);
        if (startDate > endDate)
            throw new InvalidOperationException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");

        var window = CalendarEditWindow(term);
        if (startDate < window.Start || endDate > window.End)
            throw new InvalidOperationException("Etkinlik yalnizca akademik yil duzenleme araligi icinde tasinabilir.");

        if (RequiresMondayStart(calendarEvent) && !StartsOnMonday(startDate))
            throw new InvalidOperationException("Bu etkinlik Pazartesi günü başlamalıdır.");

        if (RequiresWeekdayOnly(calendarEvent) && StartsOnWeekend(startDate))
            throw new InvalidOperationException("Sinav etkinlikleri hafta sonu baslayamaz.");

        var requestedBlockingEvents = term.CalendarEvents
            .Where(e => e.Id != calendarEvent.Id && IsBlockingCalendarEvent(e) && e.BlocksAcademicEvents)
            .Where(e => RangesOverlap(startDate, endDate, e.StartDate.Date, (e.EndDate ?? e.StartDate).Date))
            .ToList();
        if (requestedBlockingEvents.Count > 0 && !CanOverlapBlockingHoliday(calendarEvent, startDate, endDate, requestedBlockingEvents))
            throw new InvalidOperationException(requestedBlockingEvents.Any(e => e.EventType == CalendarEventType.ReligiousHoliday) ? $"{calendarEvent.Title} dini bayram üzerine taşınamaz." : $"{calendarEvent.Title} resmi tatil veya bloklayıcı önemli gün üzerine taşınamaz.");

        var currentEvents = term.CalendarEvents.Select(e => ToGeneratedEvent(term, e)).ToList();
        var holidays = BuildResolvedHolidays(term.CalendarEvents);
        var baseline = CalendarGeneratorService.EvaluateEvents(currentEvents, holidays);
        var draftEventId = calendarEvent.Id.ToString(CultureInfo.InvariantCulture);
        var pinnedDraftEventIds = new HashSet<string>(StringComparer.Ordinal) { draftEventId };
        var recalculation = CalendarGeneratorService.RecalculateEvents(
            currentEvents,
            holidays,
            draftEventId,
            startDate,
            endDate,
            false);

        var changedEventIds = ApplyRecalculatedEvents(term, recalculation.Events);
        changedEventIds.Add(calendarEvent.Id);

        RepairMovedEventDependencies(term, calendarEvent.Id, changedEventIds, holidays, pinnedDraftEventIds);
        NormalizeChangedDependentEvents(term, calendarEvent.Id, changedEventIds);

        WhatIfResultDto finalEvaluation = null!;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var before = TermDateFingerprint(term);
            var finalEvents = term.CalendarEvents.Select(e => ToGeneratedEvent(term, e)).ToList();
            finalEvaluation = CalendarGeneratorService.EvaluateEvents(finalEvents, holidays, pinnedDraftEventIds);
            foreach (var changedId in ApplyRecalculatedEvents(term, finalEvaluation.Events))
                changedEventIds.Add(changedId);

            NormalizeChangedDependentEvents(term, calendarEvent.Id, changedEventIds);
            if (TermDateFingerprint(term) == before)
                break;
        }

        var changedDraftEventIds = GetChangedDraftEventIds(currentEvents, finalEvaluation.Events);
        changedDraftEventIds.Add(draftEventId);
        foreach (var changedId in changedEventIds)
            changedDraftEventIds.Add(changedId.ToString(CultureInfo.InvariantCulture));
        ThrowIfGeneratorRejected(finalEvaluation, baseline, changedDraftEventIds);
        ValidateCalendarEventGraph(term, changedEventIds);
        return changedEventIds;
    }

    // Public view

    public async Task<StudentAcademicCalendarDto?> GetStudentViewAsync(int termId, bool includeAllEvents = false, CancellationToken ct = default)
    {
        var term = await _ctx.AcademicTerms
            .AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .Include(t => t.CalendarEvents.Where(e => includeAllEvents || e.IsPublic))
            .FirstOrDefaultAsync(t => t.Id == termId, ct);

        if (term is null)
            return null;

        return BuildStudentDto(term, _clock.UtcNow, await _institutionTimeZone.GetAsync(ct));
    }

    public async Task<StudentAcademicCalendarDto?> GetActiveStudentViewAsync(bool includeAllEvents = false, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var term = await _ctx.AcademicTerms
            .AsNoTracking()
            .Include(t => t.AcademicCalendar)
            .Include(t => t.CalendarEvents.Where(e => includeAllEvents || e.IsPublic))
            .ActiveTerm()
            .FirstOrDefaultAsync(ct);

        if (term is null)
            return null;

        return BuildStudentDto(term, now, await _institutionTimeZone.GetAsync(ct));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void Validate(UpsertAcademicCalendarDto dto, AcademicTerm term)
    {
        // Same-phase consistency
        if (dto.RegistrationStart >= dto.RegistrationEnd)
            throw new InvalidOperationException("Kayıt başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        if (dto.AddDropStart >= dto.AddDropEnd)
            throw new InvalidOperationException("Ders ekleme/bırakma başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        if (dto.GradeEntryStart >= dto.GradeEntryEnd)
            throw new InvalidOperationException("Not girişi başlangıç tarihi bitiş tarihinden önce olmalıdır.");

        // Term boundaries
        if (dto.RegistrationEnd > term.TermEnd)
            throw new InvalidOperationException("Kayıt bitiş tarihi dönem bitiş tarihinden sonra olamaz.");

        if (dto.AddDropEnd > term.TermEnd)
            throw new InvalidOperationException("Ders ekleme/bırakma bitiş tarihi dönem bitiş tarihinden sonra olamaz.");

        if (dto.WithdrawalDeadline > term.TermEnd)
            throw new InvalidOperationException("Çekilme tarihi dönem bitiş tarihinden sonra olamaz.");

        if (dto.WithdrawalDeadline < term.TermStart)
            throw new InvalidOperationException("Çekilme tarihi dönem başlangıç tarihinden önce olamaz.");

        // Phase order
        if (dto.RegistrationEnd > dto.AddDropStart)
            throw new InvalidOperationException(
                "Ekle-bırak, kayıt bitmeden başlayamaz. Kayıt bitişini ekle-bırak başlangıcından önce olacak şekilde ayarlayın.");

        if (dto.AddDropEnd > dto.GradeEntryStart)
            throw new InvalidOperationException(
                "Not girişi, ekle-bırak bitmeden başlayamaz. Ekle-bırak bitişini not girişi başlangıcından önce olacak şekilde ayarlayın.");

        if (dto.GradeEntryStart < term.TermStart)
            throw new InvalidOperationException(
                "Not girişi, dönem başlamadan başlayamaz. Not girişi başlangıcını dönem başlangıcına veya sonrasına ayarlayın.");
    }

    private static UpsertAcademicCalendarDto BuildUpsertDto(TermAcademicCalendar calendar) =>
        new(
            calendar.RegistrationStart,
            calendar.RegistrationEnd,
            calendar.AddDropStart,
            calendar.AddDropEnd,
            calendar.WithdrawalDeadline,
            calendar.GradeEntryStart,
            calendar.GradeEntryEnd);

    private static void SyncAcademicCalendarFromEvents(AcademicTerm term)
    {
        var calendar = term.AcademicCalendar;
        if (calendar is null)
            return;

        var registration = FirstEvent(term.CalendarEvents,
            CalendarEventType.CourseRegistration,
            CalendarEventType.NewStudentRegistration,
            CalendarEventType.GraduateRegistration,
            CalendarEventType.TransferRegistration,
            CalendarEventType.SummerSchoolRegistration);
        var addDrop = FirstEvent(term.CalendarEvents, CalendarEventType.AddDrop, CalendarEventType.AddDropApproval);
        var withdrawal = FirstEvent(term.CalendarEvents, CalendarEventType.CourseWithdrawal, CalendarEventType.AdvisorWithdrawal);
        var gradeEntryStart = FirstEvent(term.CalendarEvents,
            CalendarEventType.MidtermGradeEntry,
            CalendarEventType.FinalGradeEntry,
            CalendarEventType.MakeupGradeEntry,
            CalendarEventType.MidtermGradePublication,
            CalendarEventType.FinalGradePublication,
            CalendarEventType.MakeupGradePublication,
            CalendarEventType.SummerSchoolGradePublication);
        var gradeEntryEnd = LastEvent(term.CalendarEvents,
            CalendarEventType.MakeupGradeEntry,
            CalendarEventType.FinalGradeEntry,
            CalendarEventType.MakeupGradePublication,
            CalendarEventType.FinalGradePublication,
            CalendarEventType.SummerSchoolGradePublication,
            CalendarEventType.MidtermGradeEntry,
            CalendarEventType.MidtermGradePublication);

        calendar.RegistrationStart = registration?.StartDate ?? calendar.RegistrationStart;
        calendar.RegistrationEnd = registration?.EndDate ?? registration?.StartDate ?? calendar.RegistrationEnd;
        calendar.AddDropStart = addDrop?.StartDate ?? calendar.AddDropStart;
        calendar.AddDropEnd = addDrop?.EndDate ?? addDrop?.StartDate ?? calendar.AddDropEnd;
        calendar.WithdrawalDeadline = withdrawal?.EndDate ?? withdrawal?.StartDate ?? calendar.WithdrawalDeadline;
        calendar.GradeEntryStart = gradeEntryStart?.StartDate ?? calendar.GradeEntryStart;
        calendar.GradeEntryEnd = gradeEntryEnd?.EndDate ?? gradeEntryEnd?.StartDate ?? calendar.GradeEntryEnd;
    }

    private static TermCalendarEvent? FirstEvent(IEnumerable<TermCalendarEvent> events, params CalendarEventType[] eventTypes) =>
        events
            .Where(e => e.IsPublic && eventTypes.Contains(e.EventType))
            .OrderBy(e => e.StartDate)
            .FirstOrDefault();

    private static TermCalendarEvent? LastEvent(IEnumerable<TermCalendarEvent> events, params CalendarEventType[] eventTypes) =>
        events
            .Where(e => e.IsPublic && eventTypes.Contains(e.EventType))
            .OrderByDescending(e => e.EndDate ?? e.StartDate)
            .FirstOrDefault();

    private static DateTime EnsureUtcDate(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).Date;

    private static bool IsLockedCalendarEvent(TermCalendarEvent calendarEvent) =>
        IsBlockingCalendarEvent(calendarEvent);

    private static bool IsBlockingCalendarEvent(TermCalendarEvent calendarEvent) =>
        calendarEvent.EventType is CalendarEventType.NationalHoliday or CalendarEventType.ReligiousHoliday or CalendarEventType.ImportantDay;

    private static void ApplyClientEvents(AcademicTerm term, IReadOnlyList<TermCalendarEventDto> clientEvents)
    {
        if (clientEvents.Count == 0)
            throw new InvalidOperationException("Takvim etkinligi bulunamadi.");

        var termEventsById = term.CalendarEvents.ToDictionary(e => e.Id);
        var clientEventsById = clientEvents
            .GroupBy(e => e.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var unknownEventId = clientEventsById.Keys.FirstOrDefault(id => !termEventsById.ContainsKey(id));
        if (unknownEventId != 0)
            throw new InvalidOperationException($"Takvim etkinligi bu doneme ait degil: {unknownEventId}.");

        foreach (var calendarEvent in term.CalendarEvents)
        {
            if (!clientEventsById.TryGetValue(calendarEvent.Id, out var clientEvent))
                continue;
            if (IsLockedCalendarEvent(calendarEvent))
                continue;

            var startDate = EnsureUtcDate(clientEvent.StartDate);
            var endDate = EnsureUtcDate(clientEvent.EndDate ?? clientEvent.StartDate);
            if (startDate > endDate)
                throw new InvalidOperationException($"{calendarEvent.Title} icin baslangic tarihi bitis tarihinden sonra olamaz.");

            calendarEvent.StartDate = startDate;
            calendarEvent.EndDate = startDate == endDate ? null : endDate;
        }
    }

    private static void ValidateTermReadyToSave(AcademicTerm term)
    {
        var events = term.CalendarEvents.Select(e => ToGeneratedEvent(term, e)).ToList();
        var evaluation = CalendarGeneratorService.EvaluateEvents(events, BuildResolvedHolidays(term.CalendarEvents));
        ThrowIfAnyGeneratorRejected(evaluation);
        ApplyRecalculatedEvents(term, evaluation.Events);

        ValidateCalendarEventGraph(
            term,
            term.CalendarEvents
                .Where(e => e.IsPublic && !IsLockedCalendarEvent(e))
                .Select(e => e.Id)
                .ToHashSet());

        if (term.AcademicCalendar is not null)
        {
            SyncAcademicCalendarFromEvents(term);
            Validate(BuildUpsertDto(term.AcademicCalendar), term);
        }
    }

    private static GeneratedCalendarEventDto ToGeneratedEvent(AcademicTerm term, TermCalendarEvent calendarEvent) =>
        new(
            calendarEvent.Id.ToString(CultureInfo.InvariantCulture),
            SemesterFromTerm(term),
            term.Id.ToString(CultureInfo.InvariantCulture),
            calendarEvent.EventType,
            CalendarEventTypeLabels.GetLabel(calendarEvent.EventType),
            calendarEvent.Title,
            EnsureUtcDate(calendarEvent.StartDate),
            calendarEvent.EndDate.HasValue ? EnsureUtcDate(calendarEvent.EndDate.Value) : null,
            string.IsNullOrWhiteSpace(calendarEvent.ChainCode) ? "PUBLISHED" : calendarEvent.ChainCode,
            calendarEvent.TemplateItemKey,
            calendarEvent.IsPublic,
            IsLockedCalendarEvent(calendarEvent),
            calendarEvent.BlocksAcademicEvents,
            false,
            calendarEvent.Note);

    private static SemesterType SemesterFromTerm(AcademicTerm term) =>
        term.TermType switch
        {
            TermType.Spring or TermType.SpringResit or TermType.SpringOther => SemesterType.Spring,
            TermType.Summer or TermType.SummerResit or TermType.SummerOther => SemesterType.Summer,
            _ => SemesterType.Fall
        };

    private static IReadOnlyList<ResolvedHolidayDto> BuildResolvedHolidays(IEnumerable<TermCalendarEvent> events) =>
        events
            .Where(IsBlockingCalendarEvent)
            .Select(ToResolvedHoliday)
            .ToList();

    private static ResolvedHolidayDto ToResolvedHoliday(TermCalendarEvent calendarEvent)
    {
        var startDate = EnsureUtcDate(calendarEvent.StartDate);
        var endDate = EnsureUtcDate(calendarEvent.EndDate ?? calendarEvent.StartDate);
        var blocksAcademicEvents = calendarEvent.BlocksAcademicEvents
            || calendarEvent.EventType is CalendarEventType.NationalHoliday or CalendarEventType.ReligiousHoliday;

        return new ResolvedHolidayDto(
            calendarEvent.Title,
            calendarEvent.Title,
            HolidayTypeFromEvent(calendarEvent.EventType),
            startDate,
            endDate,
            (decimal)((endDate.Date - startDate.Date).TotalDays + 1),
            calendarEvent.EventType is CalendarEventType.NationalHoliday or CalendarEventType.ReligiousHoliday,
            blocksAcademicEvents,
            false,
            "AcademicCalendar");
    }

    private static HolidayType HolidayTypeFromEvent(CalendarEventType eventType) =>
        eventType switch
        {
            CalendarEventType.ReligiousHoliday => HolidayType.HijriCalculated,
            CalendarEventType.ImportantDay => HolidayType.ImportantDay,
            _ => HolidayType.Fixed
        };

    private static void ThrowIfGeneratorRejected(
        WhatIfResultDto recalculation,
        WhatIfResultDto baseline,
        IReadOnlySet<string> changedDraftEventIds)
    {
        if (recalculation.Events.Any(e => e.IsFailed && changedDraftEventIds.Contains(e.DraftEventId)))
            throw new InvalidOperationException("Takvim kurallari saglanamadigi icin bazi etkinlikler yerlestirilemedi.");

        var baselineErrors = baseline.Warnings
            .Where(IsErrorWarning)
            .Select(WarningFingerprint)
            .ToHashSet(StringComparer.Ordinal);
        var blockingWarnings = recalculation.Warnings
            .Where(IsErrorWarning)
            .Where(w => IsChangedDraftWarning(w, changedDraftEventIds)
                || (string.IsNullOrWhiteSpace(w.DraftEventId) && !baselineErrors.Contains(WarningFingerprint(w))))
            .Select(w => w.Message)
            .Distinct()
            .ToList();
        if (blockingWarnings.Count > 0)
            throw new InvalidOperationException("Takvim kurallari saglanamadi: " + string.Join(" | ", blockingWarnings));
    }

    private static void ThrowIfAnyGeneratorRejected(WhatIfResultDto evaluation)
    {
        if (evaluation.Events.Any(e => e.IsFailed))
            throw new InvalidOperationException("Takvim kurallari saglanamadigi icin bazi etkinlikler yerlestirilemedi.");

        var blockingWarnings = evaluation.Warnings
            .Where(IsErrorWarning)
            .Select(w => w.Message)
            .Distinct()
            .ToList();
        if (blockingWarnings.Count > 0)
            throw new InvalidOperationException("Takvim kaydedilemedi. Once tum hatalari duzeltin: " + string.Join(" | ", blockingWarnings));
    }

    private static bool IsErrorWarning(CalendarWarningDto warning) =>
        string.Equals(warning.Severity, "Error", StringComparison.OrdinalIgnoreCase);

    private static bool IsChangedDraftWarning(CalendarWarningDto warning, IReadOnlySet<string> changedDraftEventIds) =>
        !string.IsNullOrWhiteSpace(warning.DraftEventId) && changedDraftEventIds.Contains(warning.DraftEventId);

    private static string WarningFingerprint(CalendarWarningDto warning) =>
        $"{warning.Code}|{warning.DraftEventId}|{warning.Message}";

    private static HashSet<string> GetChangedDraftEventIds(
        IReadOnlyList<GeneratedCalendarEventDto> currentEvents,
        IReadOnlyList<GeneratedCalendarEventDto> recalculatedEvents)
    {
        var currentById = currentEvents.ToDictionary(e => e.DraftEventId, StringComparer.Ordinal);
        var changedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var recalculatedEvent in recalculatedEvents)
        {
            if (!currentById.TryGetValue(recalculatedEvent.DraftEventId, out var currentEvent))
                continue;

            if (currentEvent.StartDate.Date != recalculatedEvent.StartDate.Date
                || (currentEvent.EndDate?.Date) != (recalculatedEvent.EndDate?.Date))
                changedIds.Add(recalculatedEvent.DraftEventId);
        }

        return changedIds;
    }

    private static HashSet<int> ApplyRecalculatedEvents(AcademicTerm term, IReadOnlyList<GeneratedCalendarEventDto> events)
    {
        var changedEventIds = new HashSet<int>();
        var byId = term.CalendarEvents.ToDictionary(e => e.Id);

        foreach (var generatedEvent in events)
        {
            if (!int.TryParse(generatedEvent.DraftEventId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var eventId))
                continue;
            if (!byId.TryGetValue(eventId, out var calendarEvent))
                continue;

            var nextStartDate = EnsureUtcDate(generatedEvent.StartDate);
            var nextEndDate = generatedEvent.EndDate.HasValue ? EnsureUtcDate(generatedEvent.EndDate.Value) : (DateTime?)null;
            if (calendarEvent.StartDate.Date == nextStartDate.Date
                && (calendarEvent.EndDate?.Date) == (nextEndDate?.Date))
                continue;

            calendarEvent.StartDate = nextStartDate;
            calendarEvent.EndDate = nextEndDate;
            changedEventIds.Add(calendarEvent.Id);
        }

        return changedEventIds;
    }

    private static bool CanOverlapBlockingHoliday(
        TermCalendarEvent calendarEvent,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<TermCalendarEvent> blockingEvents)
    {
        if (calendarEvent.EventType is CalendarEventType.CourseRegistration or CalendarEventType.AddDrop)
            return true;

        if (RequiresWeekdayOnly(calendarEvent) || IsStrictHolidayBlocked(calendarEvent.EventType) || InclusiveDays(startDate, endDate) < FlexibleHolidayOverlapMinDays)
            return false;

        return blockingEvents.Count > 0 && blockingEvents.All(e => e.EventType != CalendarEventType.ReligiousHoliday);
    }

    private static bool IsStrictHolidayBlocked(CalendarEventType eventType) =>
        eventType is CalendarEventType.ClassesStart
            or CalendarEventType.ClassesEnd
            or CalendarEventType.CourseWithdrawal
            or CalendarEventType.RegistrationFreezeDeadline
            or CalendarEventType.MidtermExam
            or CalendarEventType.FinalExam
            or CalendarEventType.MakeupExamApplication
            or CalendarEventType.MakeupExam
            or CalendarEventType.ExcuseExam
            or CalendarEventType.MidtermGradePublication
            or CalendarEventType.FinalGradePublication
            or CalendarEventType.MakeupGradePublication
            or CalendarEventType.SummerSchoolRegistration
            or CalendarEventType.SummerSchoolExam
            or CalendarEventType.SummerSchoolGradePublication;

    private static bool AffectsAcademicCalendarSummary(CalendarEventType eventType) =>
        eventType is CalendarEventType.CourseRegistration
            or CalendarEventType.NewStudentRegistration
            or CalendarEventType.GraduateRegistration
            or CalendarEventType.TransferRegistration
            or CalendarEventType.SummerSchoolRegistration
            or CalendarEventType.AddDrop
            or CalendarEventType.AddDropApproval
            or CalendarEventType.CourseWithdrawal
            or CalendarEventType.AdvisorWithdrawal
            or CalendarEventType.MidtermGradeEntry
            or CalendarEventType.FinalGradeEntry
            or CalendarEventType.MakeupGradeEntry
            or CalendarEventType.MidtermGradePublication
            or CalendarEventType.FinalGradePublication
            or CalendarEventType.MakeupGradePublication
            or CalendarEventType.SummerSchoolGradePublication;

    private static bool RequiresWeekdayOnly(CalendarEventType eventType) =>
        eventType is CalendarEventType.MidtermExam
            or CalendarEventType.FinalExam
            or CalendarEventType.MakeupExam
            or CalendarEventType.ExcuseExam
            or CalendarEventType.PrepSchoolExam
            or CalendarEventType.SummerSchoolExam;

    private static bool RequiresWeekdayOnly(TermCalendarEvent calendarEvent) =>
        !IsMakeupApplication(calendarEvent) && RequiresWeekdayOnly(calendarEvent.EventType);

    private static bool RequiresMondayStart(CalendarEventType eventType) =>
        eventType is CalendarEventType.ClassesStart
            or CalendarEventType.CourseRegistration
            or CalendarEventType.AddDrop
            or CalendarEventType.MidtermExam
            or CalendarEventType.FinalExam
            or CalendarEventType.MakeupExam
            or CalendarEventType.PrepSchoolExam
            or CalendarEventType.SummerSchoolExam;

    private static bool RequiresMondayStart(TermCalendarEvent calendarEvent) =>
        !IsMakeupApplication(calendarEvent) && RequiresMondayStart(calendarEvent.EventType);

    private static bool IsMakeupApplication(TermCalendarEvent calendarEvent) =>
        MatchesKey(calendarEvent, "makeup_application")
        || calendarEvent.EventType == CalendarEventType.MakeupExam && TitleContainsAny(calendarEvent, "Basvuru", "Başvuru", "Başvuru");

    private static bool ShouldTrimTrailingWeekend(TermCalendarEvent calendarEvent) =>
        !IsLockedCalendarEvent(calendarEvent)
        && (!IsStrictHolidayBlocked(calendarEvent.EventType) || RequiresWeekdayOnly(calendarEvent));

    private static DateTime TrimTrailingWeekendEnd(TermCalendarEvent calendarEvent, DateTime startDate, DateTime endDate)
    {
        if (!ShouldTrimTrailingWeekend(calendarEvent) || endDate.Date <= startDate.Date)
            return endDate.Date;

        var trimmedEnd = endDate.Date;
        while (trimmedEnd > startDate.Date && trimmedEnd.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            trimmedEnd = trimmedEnd.AddDays(-1);

        return EnsureUtcDate(trimmedEnd);
    }

    private static void EnsureNoBlockingOverlap(IEnumerable<TermCalendarEvent> events, TermCalendarEvent movingEvent, DateTime startDate, DateTime endDate)
    {
        var blockingEvent = events
            .Where(e => e.Id != movingEvent.Id && IsBlockingCalendarEvent(e) && e.BlocksAcademicEvents)
            .FirstOrDefault(e => RangesOverlap(startDate, endDate, e.StartDate.Date, (e.EndDate ?? e.StartDate).Date));

        if (blockingEvent is null)
            return;

        if (CanOverlapBlockingHoliday(movingEvent, startDate, endDate, [blockingEvent]))
            return;

        throw new InvalidOperationException(blockingEvent.EventType == CalendarEventType.ReligiousHoliday ? $"{movingEvent.Title} dini bayram üzerine taşınamaz." : $"{movingEvent.Title} resmi tatil veya bloklayıcı önemli gün üzerine taşınamaz.");
    }

    private static bool RangesOverlap(DateTime leftStart, DateTime leftEnd, DateTime rightStart, DateTime rightEnd) =>
        leftStart <= rightEnd && rightStart <= leftEnd;

    private static void RepairMovedEventDependencies(
        AcademicTerm term,
        int movedEventId,
        ISet<int> changedEventIds,
        IReadOnlyList<ResolvedHolidayDto> holidays,
        IReadOnlySet<string> pinnedDraftEventIds)
    {
        var movedEvent = term.CalendarEvents.FirstOrDefault(e => e.Id == movedEventId);
        if (movedEvent is null)
            return;

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var before = TermDateFingerprint(term);
            ApplyDependentEventRules(term, movedEvent, changedEventIds);

            var evaluation = CalendarGeneratorService.EvaluateEvents(
                term.CalendarEvents.Select(e => ToGeneratedEvent(term, e)).ToList(),
                holidays,
                pinnedDraftEventIds);
            foreach (var changedId in ApplyRecalculatedEvents(term, evaluation.Events))
                changedEventIds.Add(changedId);

            if (TermDateFingerprint(term) == before)
                break;
        }
    }

    private static void NormalizeChangedDependentEvents(AcademicTerm term, int movedEventId, ISet<int> changedEventIds)
    {
        foreach (var calendarEvent in term.CalendarEvents
            .Where(e => e.Id != movedEventId && e.IsPublic && !IsLockedCalendarEvent(e) && changedEventIds.Contains(e.Id))
            .OrderBy(e => e.StartDate)
            .ThenBy(e => e.Id))
        {
            var startDate = calendarEvent.StartDate.Date;
            DateTime? alignedStart = null;

            if (RequiresMondayStart(calendarEvent) && !StartsOnMonday(startDate))
                alignedStart = MondayOnOrAfter(startDate);
            else if (RequiresWeekdayOnly(calendarEvent) && StartsOnWeekend(startDate))
                alignedStart = NextBusinessDayOnOrAfter(startDate, term.CalendarEvents);

            if (alignedStart.HasValue && alignedStart.Value.Date != startDate)
                SetPreservingSpan(calendarEvent, alignedStart.Value, changedEventIds);

            var trimmedEnd = TrimTrailingWeekendEnd(calendarEvent, calendarEvent.StartDate.Date, EventEnd(calendarEvent));
            if (trimmedEnd.Date != EventEnd(calendarEvent).Date)
            {
                calendarEvent.EndDate = trimmedEnd.Date == calendarEvent.StartDate.Date ? null : EnsureUtcDate(trimmedEnd);
                changedEventIds.Add(calendarEvent.Id);
            }
        }
    }

    private static string TermDateFingerprint(AcademicTerm term) =>
        string.Join("|", term.CalendarEvents
            .OrderBy(e => e.Id)
            .Select(e => $"{e.Id}:{e.StartDate:yyyyMMdd}:{(e.EndDate.HasValue ? e.EndDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : string.Empty)}"));

    private static void ApplyDependentEventRules(AcademicTerm term, TermCalendarEvent movedEvent, ISet<int> changedEventIds)
    {
        ApplyMidtermDependencies(term, movedEvent, changedEventIds);
        ApplyFinalAndMakeupDependencies(term, movedEvent, changedEventIds);
        ApplySummerDependencies(term, movedEvent, changedEventIds);
    }

    private static void ApplyMidtermDependencies(AcademicTerm term, TermCalendarEvent movedEvent, ISet<int> changedEventIds)
    {
        var movedMidterm = IsMidtermExamEvent(movedEvent);
        var movedExcuseExam = IsMidtermExcuseExamEvent(movedEvent);
        if (!movedMidterm && !movedExcuseExam)
            return;

        var midterm = FindMidtermExam(term);
        if (midterm is null)
            return;

        var midtermEnd = EventEnd(midterm);

        if (movedMidterm)
        {
            var excuseApplication = FindMidtermExcuseApplication(term);
            if (excuseApplication is not null)
                SetSingleDay(excuseApplication, NextBusinessDayAfter(midtermEnd, term.CalendarEvents), changedEventIds);

            var excuseExam = FindMidtermExcuseExam(term);
            if (excuseExam is not null)
                SetPreservingSpan(excuseExam, NthBusinessDayAfter(midtermEnd, ExcuseExamMinBusinessDays, term.CalendarEvents), changedEventIds);
        }

        var refreshedExcuseExam = FindMidtermExcuseExam(term);
        var publication = FindEventByKeyOrType(term, "midterm_publication", CalendarEventType.MidtermGradePublication);
        if (refreshedExcuseExam is null || publication is null)
            return;

        var excuseEnd = EventEnd(refreshedExcuseExam);
        var earliestPublication = NextBusinessDayAfter(excuseEnd, term.CalendarEvents);
        var preferredPublication = NthBusinessDayAfter(excuseEnd, DependentPublicationBusinessDays, term.CalendarEvents);
        var latestAllowedPublication = NthBusinessDayAfter(midtermEnd, GradePublicationBusinessDayLimit, term.CalendarEvents);
        var desiredPublication = preferredPublication.Date > latestAllowedPublication.Date
            ? (latestAllowedPublication.Date >= earliestPublication.Date ? latestAllowedPublication : earliestPublication)
            : preferredPublication;

        SetSingleDay(publication, desiredPublication, changedEventIds);
    }

    private static void ApplyFinalAndMakeupDependencies(AcademicTerm term, TermCalendarEvent movedEvent, ISet<int> changedEventIds)
    {
        var movedFinal = MatchesKey(movedEvent, "final_exam") || movedEvent.EventType == CalendarEventType.FinalExam;
        var movedFinalPublication = MatchesKey(movedEvent, "final_publication") || movedEvent.EventType == CalendarEventType.FinalGradePublication;
        var movedMakeup = IsMakeupExamEvent(movedEvent);
        if (!movedFinal && !movedFinalPublication && !movedMakeup)
            return;

        var finalExam = FindEventByKeyOrType(term, "final_exam", CalendarEventType.FinalExam);
        if (finalExam is null)
            return;

        var finalEnd = EventEnd(finalExam);
        var finalPublication = FindEventByKeyOrType(term, "final_publication", CalendarEventType.FinalGradePublication);
        if (movedFinal && finalPublication is not null)
            SetSingleDay(finalPublication, NthBusinessDayAfter(finalEnd, DependentPublicationBusinessDays, term.CalendarEvents), changedEventIds);

        finalPublication = FindEventByKeyOrType(term, "final_publication", CalendarEventType.FinalGradePublication);
        var makeupExam = FindMakeupExam(term);
        if (makeupExam is null)
            return;

        if (movedFinal || movedFinalPublication)
        {
            var fifthBusinessDay = NthBusinessDayAfter(finalEnd, MakeupExamMinBusinessDays, term.CalendarEvents);
            var publicationBuffer = (finalPublication?.StartDate ?? finalEnd).AddDays(MakeupAfterPublicationBufferDays);
            SetPreservingSpan(makeupExam, AlignMakeupStart(MaxDate(fifthBusinessDay, publicationBuffer), term.CalendarEvents), changedEventIds);
        }

        var makeupApplication = FindMakeupApplication(term);
        if (makeupApplication is not null)
            SetSingleDay(makeupApplication, PreviousBusinessDayBefore(makeupExam.StartDate, term.CalendarEvents), changedEventIds);

        var makeupPublication = FindEventByKeyOrType(term, "makeup_publication", CalendarEventType.MakeupGradePublication);
        if (makeupPublication is not null)
            SetSingleDay(makeupPublication, NthBusinessDayAfter(EventEnd(makeupExam), DependentPublicationBusinessDays, term.CalendarEvents), changedEventIds);
    }

    private static void ApplySummerDependencies(AcademicTerm term, TermCalendarEvent movedEvent, ISet<int> changedEventIds)
    {
        var movedSummerExam = MatchesKey(movedEvent, "summer_exam") || movedEvent.EventType == CalendarEventType.SummerSchoolExam;
        var movedSummerExcuse = MatchesKey(movedEvent, "summer_excuse_exam")
            || (movedEvent.EventType == CalendarEventType.ExcuseExam && string.Equals(movedEvent.ChainCode, "SUMMER", StringComparison.OrdinalIgnoreCase));
        if (!movedSummerExam && !movedSummerExcuse)
            return;

        var summerExam = FindEventByKeyOrType(term, "summer_exam", CalendarEventType.SummerSchoolExam);
        if (summerExam is null)
            return;

        if (movedSummerExam)
        {
            var application = FindEventByKeyOrType(term, "summer_excuse_application", CalendarEventType.ExcuseExamApplication);
            if (application is not null)
                SetSingleDay(application, NextBusinessDayAfter(EventEnd(summerExam), term.CalendarEvents), changedEventIds);

            var excuseExam = FindEventByKeyOrType(term, "summer_excuse_exam", CalendarEventType.ExcuseExam);
            if (excuseExam is not null)
                SetPreservingSpan(excuseExam, NthBusinessDayAfter(EventEnd(summerExam), ExcuseExamMinBusinessDays, term.CalendarEvents), changedEventIds);
        }

        var refreshedExcuse = FindEventByKeyOrType(term, "summer_excuse_exam", CalendarEventType.ExcuseExam);
        var publication = FindEventByKeyOrType(term, "summer_publication", CalendarEventType.SummerSchoolGradePublication);
        if (publication is not null)
            SetSingleDay(publication, NthBusinessDayAfter(EventEnd(refreshedExcuse ?? summerExam), DependentPublicationBusinessDays, term.CalendarEvents), changedEventIds);
    }

    private static void ValidateCalendarEventGraph(AcademicTerm term, IReadOnlySet<int> changedEventIds)
    {
        foreach (var calendarEvent in term.CalendarEvents.Where(e => e.IsPublic && !IsLockedCalendarEvent(e) && changedEventIds.Contains(e.Id)))
        {
            var startDate = calendarEvent.StartDate.Date;
            var endDate = EventEnd(calendarEvent);

            if (startDate > endDate)
                throw new InvalidOperationException($"{calendarEvent.Title} icin baslangic tarihi bitis tarihinden sonra olamaz.");

            var window = CalendarEditWindow(term);
            if (startDate < window.Start || endDate > window.End)
                throw new InvalidOperationException($"{calendarEvent.Title} yalnizca akademik yil duzenleme araligi icinde tasinabilir.");

            if (RequiresMondayStart(calendarEvent) && !StartsOnMonday(startDate))
                throw new InvalidOperationException($"{calendarEvent.Title} Pazartesi günü başlamalıdır.");

            if (RequiresWeekdayOnly(calendarEvent) && StartsOnWeekend(startDate))
                throw new InvalidOperationException("Sinav etkinlikleri hafta sonu baslayamaz.");

            EnsureNoBlockingOverlap(term.CalendarEvents, calendarEvent, startDate, endDate);
        }

        EnsureEventOrder(FindEventByKeyOrType(term, "classes_start", CalendarEventType.ClassesStart),
            FindEventByKeyOrType(term, "classes_end", CalendarEventType.ClassesEnd),
            "Derslerin baslangici derslerin bitisinden sonra olamaz.",
            changedEventIds);

        EnsureEventOrder(FindMidtermExam(term),
            FindEventByKeyOrType(term, "final_exam", CalendarEventType.FinalExam),
            "Ara sinavlar donem sonu sinavlarindan sonra olamaz.",
            changedEventIds);

        var midterm = FindMidtermExam(term);
        var midtermExcuse = FindMidtermExcuseExam(term);
        var midtermPublication = FindEventByKeyOrType(term, "midterm_publication", CalendarEventType.MidtermGradePublication);
        EnsureEventOrder(midterm, midtermExcuse, "Mazeret sinavi ara sinavlardan once olamaz.", changedEventIds);
        EnsureEventOrder(midtermExcuse, midtermPublication, "Ara sinav ilan tarihi mazeret sinavindan once olamaz.", changedEventIds);
        if (midterm is not null && midtermPublication is not null && EventsImpacted(changedEventIds, midterm, midtermPublication))
        {
            var businessDays = CountBusinessDaysAfter(EventEnd(midterm), midtermPublication.StartDate, term.CalendarEvents);
            if (businessDays > GradePublicationBusinessDayLimit)
                throw new InvalidOperationException($"Ara sinav ilan tarihi sinav donemini izleyen en gec {GradePublicationBusinessDayLimit} is gunu icinde olmalidir.");
        }

        var finalExam = FindEventByKeyOrType(term, "final_exam", CalendarEventType.FinalExam);
        var finalPublication = FindEventByKeyOrType(term, "final_publication", CalendarEventType.FinalGradePublication);
        var makeupExam = FindMakeupExam(term);
        var makeupPublication = FindEventByKeyOrType(term, "makeup_publication", CalendarEventType.MakeupGradePublication);
        EnsureEventOrder(finalExam, finalPublication, "Final ilan tarihi final sinavindan once olamaz.", changedEventIds);
        EnsureEventOrder(finalPublication, makeupExam, "Butunleme sinavi final ilanindan once olamaz.", changedEventIds);
        EnsureEventOrder(makeupExam, makeupPublication, "Butunleme ilan tarihi butunleme sinavindan once olamaz.", changedEventIds);
    }

    private static void EnsureEventOrder(TermCalendarEvent? first, TermCalendarEvent? second, string message, IReadOnlySet<int> changedEventIds)
    {
        if (first is null || second is null)
            return;

        if (!EventsImpacted(changedEventIds, first, second))
            return;

        if (EventEnd(first).Date > second.StartDate.Date)
            throw new InvalidOperationException(message);
    }

    private static bool EventsImpacted(IReadOnlySet<int> changedEventIds, params TermCalendarEvent?[] events) =>
        events.Any(e => e is not null && changedEventIds.Contains(e.Id));

    private static int InclusiveDays(DateTime startDate, DateTime endDate) =>
        Math.Max(0, (endDate.Date - startDate.Date).Days + 1);

    private static TermCalendarEvent? FindMidtermExam(AcademicTerm term) =>
        term.CalendarEvents
            .Where(e => e.IsPublic)
            .OrderBy(e => e.StartDate)
            .FirstOrDefault(IsMidtermExamEvent);

    private static TermCalendarEvent? FindMidtermExcuseApplication(AcademicTerm term) =>
        FindEventByKeyOrType(term, "midterm_excuse_application", CalendarEventType.ExcuseExamApplication, "Mazeret", "Basvuru", "Başvuru");

    private static TermCalendarEvent? FindMidtermExcuseExam(AcademicTerm term) =>
        term.CalendarEvents
            .Where(e => e.IsPublic)
            .OrderBy(e => e.StartDate)
            .FirstOrDefault(IsMidtermExcuseExamEvent);

    private static TermCalendarEvent? FindMakeupApplication(AcademicTerm term) =>
        FindEventByKeyOrType(term, "makeup_application", CalendarEventType.MakeupExamApplication, "Basvuru", "Başvuru")
        ?? term.CalendarEvents
            .Where(e => e.IsPublic)
            .OrderBy(e => e.StartDate)
            .FirstOrDefault(e => e.EventType == CalendarEventType.MakeupExam && (e.EndDate is null || TitleContainsAny(e, "Basvuru", "Başvuru")));

    private static TermCalendarEvent? FindMakeupExam(AcademicTerm term) =>
        term.CalendarEvents
            .Where(e => e.IsPublic)
            .OrderBy(e => e.StartDate)
            .FirstOrDefault(IsMakeupExamEvent);

    private static TermCalendarEvent? FindEventByKeyOrType(AcademicTerm term, string templateItemKey, CalendarEventType eventType, params string[] titleFragments)
    {
        var events = term.CalendarEvents.Where(e => e.IsPublic).OrderBy(e => e.StartDate).ToList();
        return events.FirstOrDefault(e => MatchesKey(e, templateItemKey))
            ?? events.FirstOrDefault(e => e.EventType == eventType && (titleFragments.Length == 0 || TitleContainsAny(e, titleFragments)));
    }

    private static bool IsMidtermExamEvent(TermCalendarEvent calendarEvent) =>
        MatchesKey(calendarEvent, "midterm_exam")
        || (calendarEvent.EventType == CalendarEventType.MidtermExam && !TitleContainsAny(calendarEvent, "Mazeret"));

    private static bool IsMidtermExcuseExamEvent(TermCalendarEvent calendarEvent) =>
        MatchesKey(calendarEvent, "midterm_excuse_exam")
        || calendarEvent.EventType == CalendarEventType.ExcuseExam && !string.Equals(calendarEvent.ChainCode, "SUMMER", StringComparison.OrdinalIgnoreCase)
        || calendarEvent.EventType == CalendarEventType.MidtermExam && TitleContainsAny(calendarEvent, "Mazeret");

    private static bool IsMakeupExamEvent(TermCalendarEvent calendarEvent) =>
        MatchesKey(calendarEvent, "makeup_exam")
        || (calendarEvent.EventType == CalendarEventType.MakeupExam && calendarEvent.EndDate is not null && !TitleContainsAny(calendarEvent, "Basvuru", "Başvuru"));

    private static bool MatchesKey(TermCalendarEvent calendarEvent, string templateItemKey) =>
        string.Equals(calendarEvent.TemplateItemKey, templateItemKey, StringComparison.OrdinalIgnoreCase);

    private static bool TitleContainsAny(TermCalendarEvent calendarEvent, params string[] fragments) =>
        fragments.Any(fragment => calendarEvent.Title.Contains(fragment, StringComparison.CurrentCultureIgnoreCase));

    private static DateTime EventEnd(TermCalendarEvent calendarEvent) =>
        (calendarEvent.EndDate ?? calendarEvent.StartDate).Date;

    private static void SetSingleDay(TermCalendarEvent calendarEvent, DateTime date, ISet<int> changedEventIds)
    {
        changedEventIds.Add(calendarEvent.Id);
        calendarEvent.StartDate = EnsureUtcDate(date);
        calendarEvent.EndDate = null;
    }

    private static void SetPreservingSpan(TermCalendarEvent calendarEvent, DateTime startDate, ISet<int> changedEventIds)
    {
        changedEventIds.Add(calendarEvent.Id);
        var endDate = RequiresWeekdayOnly(calendarEvent)
            ? AddWeekdays(startDate.Date, CountWeekdays(calendarEvent.StartDate.Date, EventEnd(calendarEvent)) - 1)
            : TrimTrailingWeekendEnd(calendarEvent, startDate.Date, startDate.Date.AddDays((EventEnd(calendarEvent) - calendarEvent.StartDate.Date).Days));
        calendarEvent.StartDate = EnsureUtcDate(startDate);
        calendarEvent.EndDate = endDate.Date == startDate.Date ? null : EnsureUtcDate(endDate);
    }

    private static int CountWeekdays(DateTime startDate, DateTime endDate)
    {
        var count = 0;
        for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        }

        return Math.Max(1, count);
    }

    private static DateTime AddWeekdays(DateTime startDate, int daysToAdd)
    {
        var remaining = Math.Max(0, daysToAdd);
        var day = startDate.Date;
        while (remaining > 0)
        {
            day = day.AddDays(1);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            remaining--;
        }

        return EnsureUtcDate(day);
    }

    private static DateTime MaxDate(DateTime left, DateTime right) =>
        left.Date >= right.Date ? left.Date : right.Date;

    private static (DateTime Start, DateTime End) CalendarEditWindow(AcademicTerm term) =>
        (EnsureUtcDate(new DateTime(term.Year, 6, 1)), EnsureUtcDate(new DateTime(term.Year + 1, 9, 30)));

    private static bool StartsOnWeekend(DateTime date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static bool StartsOnMonday(DateTime date) =>
        date.DayOfWeek == DayOfWeek.Monday;

    private static DateTime MondayOnOrAfter(DateTime date)
    {
        var current = date.Date;
        while (!StartsOnMonday(current))
            current = current.AddDays(1);

        return EnsureUtcDate(current);
    }

    private static bool IsBusinessDay(DateTime date, IEnumerable<TermCalendarEvent> events)
    {
        if (StartsOnWeekend(date))
            return false;

        return !events.Any(e => IsBlockingCalendarEvent(e) && e.BlocksAcademicEvents && RangesOverlap(date.Date, date.Date, e.StartDate.Date, EventEnd(e)));
    }

    private static DateTime NextBusinessDayOnOrAfter(DateTime date, IEnumerable<TermCalendarEvent> events)
    {
        var eventList = events.ToList();
        var day = date.Date;
        while (!IsBusinessDay(day, eventList))
            day = day.AddDays(1);

        return EnsureUtcDate(day);
    }

    private static DateTime NextBusinessDayAfter(DateTime fromExclusive, IEnumerable<TermCalendarEvent> events) =>
        NthBusinessDayAfter(fromExclusive, 1, events);

    private static DateTime NthBusinessDayAfter(DateTime fromExclusive, int businessDayCount, IEnumerable<TermCalendarEvent> events)
    {
        var count = 0;
        var day = fromExclusive.Date;
        var eventList = events.ToList();
        while (count < businessDayCount)
        {
            day = day.AddDays(1);
            if (!IsBusinessDay(day, eventList))
                continue;

            count++;
        }

        return EnsureUtcDate(day);
    }

    private static DateTime PreviousBusinessDayBefore(DateTime date, IEnumerable<TermCalendarEvent> events)
    {
        var eventList = events.ToList();
        var day = date.Date.AddDays(-1);
        while (!IsBusinessDay(day, eventList))
            day = day.AddDays(-1);

        return EnsureUtcDate(day);
    }

    private static DateTime AlignMakeupStart(DateTime candidate, IEnumerable<TermCalendarEvent> events)
    {
        var eventList = events.ToList();
        var current = candidate.Date;
        while (!IsBusinessDay(current, eventList))
            current = current.AddDays(1);

        if (current.DayOfWeek == DayOfWeek.Friday)
            current = current.AddDays(3);

        return EnsureUtcDate(current);
    }

    private static int CountBusinessDaysAfter(DateTime fromExclusive, DateTime toInclusive, IEnumerable<TermCalendarEvent> events)
    {
        var count = 0;
        var eventList = events.ToList();
        for (var day = fromExclusive.Date.AddDays(1); day <= toInclusive.Date; day = day.AddDays(1))
        {
            if (IsBusinessDay(day, eventList))
                count++;
        }

        return count;
    }

    private AcademicCalendarDto ToDto(AcademicTerm term, TermAcademicCalendar cal, TimeZoneInfo institutionTimeZone)
    {
        var now = _clock.UtcNow;
        return new AcademicCalendarDto(
            Id:                  cal.Id,
            TermId:              term.Id,
            TermName:            term.Name,
            TermStart:           term.TermStart,
            TermEnd:             term.TermEnd,
            RegistrationStart:   cal.RegistrationStart,
            RegistrationEnd:     cal.RegistrationEnd,
            AddDropStart:        cal.AddDropStart,
            AddDropEnd:          cal.AddDropEnd,
            WithdrawalDeadline:  cal.WithdrawalDeadline,
            GradeEntryStart:     cal.GradeEntryStart,
            GradeEntryEnd:       cal.GradeEntryEnd,
            RegistrationOverride: cal.RegistrationOverride,
            AddDropOverride:     cal.AddDropOverride,
            WithdrawalOverride:  cal.WithdrawalOverride,
            GradeEntryOverride:  cal.GradeEntryOverride,
            IsRegistrationOpen:  term.IsRegistrationOpen(now),
            IsAddDropOpen:       term.IsAddDropOpen(now),
            IsWithdrawalOpen:    term.IsWithdrawalOpen(now, institutionTimeZone),
            IsGradeEntryOpen:    term.IsGradeEntryOpen(now),
            CreatedAt:           cal.CreatedAt,
            UpdatedAt:           cal.UpdatedAt
        );
    }

    private static StudentAcademicCalendarDto BuildStudentDto(AcademicTerm term, DateTime now, TimeZoneInfo institutionTimeZone)
    {
        var cal = term.AcademicCalendar;

        var events = term.CalendarEvents
            .OrderBy(e => e.StartDate)
            .Select(e => new TermCalendarEventDto(
                e.Id,
                e.EventType.ToString(),
                e.Title,
                e.StartDate,
                e.EndDate,
                e.Note,
                e.IsPublic,
                e.IsGenerated,
                e.BlocksAcademicEvents,
                e.ChainCode,
                e.TemplateItemKey
            ))
            .ToList();

        return new StudentAcademicCalendarDto(
            TermId:            term.Id,
            TermName:          term.Name,
            TermTypeLabel:     TermLabels.Get(term.TermType),
            Year:              term.Year,
            IsCancelled:       term.IsCancelled,
            TermStart:         term.TermStart,
            TermEnd:           term.TermEnd,
            RegistrationStart: cal?.RegistrationStart,
            RegistrationEnd:   cal?.RegistrationEnd,
            AddDropStart:      cal?.AddDropStart,
            AddDropEnd:        cal?.AddDropEnd,
            WithdrawalDeadline:cal?.WithdrawalDeadline,
            GradeEntryStart:   cal?.GradeEntryStart,
            GradeEntryEnd:     cal?.GradeEntryEnd,
            IsRegistrationOpen: term.IsRegistrationOpen(now),
            IsAddDropOpen:      term.IsAddDropOpen(now),
            IsWithdrawalOpen:   term.IsWithdrawalOpen(now, institutionTimeZone),
            IsGradeEntryOpen:   term.IsGradeEntryOpen(now),
            Events:            events
        );
    }
}

