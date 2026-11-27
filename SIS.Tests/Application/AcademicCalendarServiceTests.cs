using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.Common.Time;
using SIS.Application.AcademicCalendar.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;
using SIS.Tests.TestDoubles;

namespace SIS.Tests.Application;

public class AcademicCalendarServiceTests
{
    // ── Factory helpers ───────────────────────────────────────────────

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AcalTest-{Guid.NewGuid()}")
            .Options);

    private static AcademicCalendarService CreateService(AppDbContext ctx, DateTime? now = null, IInstitutionTimeZoneProvider? timeZone = null)
    {
        var clock = new FakeClock { UtcNow = now ?? new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc) };
        return new AcademicCalendarService(ctx, clock, timeZone ?? new SabitKurumSaatDilimi("Etc/UTC"));
    }

    private static AcademicTerm SeedTerm(AppDbContext ctx, bool cancelled = false)
    {
        var term = new AcademicTerm
        {
            Name = "2025-2026 Güz",
            ShortName = "2025G",
            EnglishName = "2025-2026 Fall",
            ShortEnglishName = "25F",
            Year = 2025,
            TermType = TermType.Fall,
            TermStart = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd   = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            IsCancelled = cancelled,
            CreatedAt = DateTime.UtcNow
        };
        ctx.AcademicTerms.Add(term);
        ctx.SaveChanges();
        return term;
    }

    private static UpsertAcademicCalendarDto ValidDto(AcademicTerm t) => new(
        RegistrationStart:  t.TermStart.AddDays(-10),
        RegistrationEnd:    t.TermStart,
        AddDropStart:       t.TermStart,
        AddDropEnd:         t.TermStart.AddDays(14),
        WithdrawalDeadline: t.TermEnd.AddDays(-30),
        GradeEntryStart:    t.TermEnd,
        GradeEntryEnd:      t.TermEnd.AddDays(14));

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    // ── Tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByTermIdAsync_ReturnsNull_WhenCalendarMissing()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetByTermIdAsync(term.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTermIdAsync_ThrowsNotFound_WhenTermMissing()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);

        Func<Task> act = async () => await svc.GetByTermIdAsync(999);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpsertAsync_CreatesCalendar_WhenNone()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        var result = await svc.UpsertAsync(term.Id, ValidDto(term));

        result.Should().NotBeNull();
        result.TermId.Should().Be(term.Id);
        ctx.TermAcademicCalendars.Count(c => c.TermId == term.Id).Should().Be(1);
    }

    // Bozuk kurum saat dilimi ayarı kayıttan önce hata verir: istek reddedilir, takvim yazılmaz.
    [Fact]
    public async Task UpsertAsync_Throws_AndSavesNothing_WhenInstitutionTimeZoneInvalid()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        ctx.SystemConfigs.Add(new SystemConfig { Key = "Institution.TimeZone", Value = "Mars/Olympus", DataType = "string" });
        ctx.SaveChanges();
        var timeZone = new InstitutionTimeZoneProvider(new SystemConfigProvider(ctx, new MemoryCache(new MemoryCacheOptions())));

        await FluentActions.Awaiting(() => CreateService(ctx, timeZone: timeZone).UpsertAsync(term.Id, ValidDto(term)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Kurum saat dilimi*");
        ctx.TermAcademicCalendars.Count().Should().Be(0);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesCalendar_WhenExists()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        // First upsert
        await svc.UpsertAsync(term.Id, ValidDto(term));

        // Second upsert with different dates
        var newDto = new UpsertAcademicCalendarDto(
            RegistrationStart:  term.TermStart.AddDays(-15),
            RegistrationEnd:    term.TermStart.AddDays(-1),
            AddDropStart:       term.TermStart,
            AddDropEnd:         term.TermStart.AddDays(10),
            WithdrawalDeadline: term.TermEnd.AddDays(-40),
            GradeEntryStart:    term.TermEnd,
            GradeEntryEnd:      term.TermEnd.AddDays(14));

        await svc.UpsertAsync(term.Id, newDto);

        ctx.TermAcademicCalendars.Count(c => c.TermId == term.Id).Should().Be(1);
        var cal = ctx.TermAcademicCalendars.Single(c => c.TermId == term.Id);
        cal.RegistrationStart.Should().Be(DateTime.SpecifyKind(term.TermStart.AddDays(-15), DateTimeKind.Utc));
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenTermCancelled()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx, cancelled: true);
        var svc = CreateService(ctx);

        Func<Task> act = async () => await svc.UpsertAsync(term.Id, ValidDto(term));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*iptal*");
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenRegistrationEndBeforeStart()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        var dto = new UpsertAcademicCalendarDto(
            RegistrationStart:  term.TermStart.AddDays(5),   // start AFTER end → invalid
            RegistrationEnd:    term.TermStart.AddDays(1),
            AddDropStart:       term.TermStart,
            AddDropEnd:         term.TermStart.AddDays(14),
            WithdrawalDeadline: term.TermEnd.AddDays(-30),
            GradeEntryStart:    term.TermEnd,
            GradeEntryEnd:      term.TermEnd.AddDays(14));

        Func<Task> act = async () => await svc.UpsertAsync(term.Id, dto);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenWithdrawalDeadlineAfterTermEnd()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        var dto = new UpsertAcademicCalendarDto(
            RegistrationStart:  term.TermStart.AddDays(-10),
            RegistrationEnd:    term.TermStart,
            AddDropStart:       term.TermStart,
            AddDropEnd:         term.TermStart.AddDays(14),
            WithdrawalDeadline: term.TermEnd.AddDays(5),    // AFTER TermEnd → invalid
            GradeEntryStart:    term.TermEnd,
            GradeEntryEnd:      term.TermEnd.AddDays(14));

        Func<Task> act = async () => await svc.UpsertAsync(term.Id, dto);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenAddDropStartsBeforeRegistrationEnds()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        // Kayıt bitişi, ekle-bırak başlangıcından SONRA → mantıksal çelişki
        var dto = new UpsertAcademicCalendarDto(
            RegistrationStart:  term.TermStart.AddDays(-10),
            RegistrationEnd:    term.TermStart.AddDays(5),
            AddDropStart:       term.TermStart,                 // RegistrationEnd'den ÖNCE
            AddDropEnd:         term.TermStart.AddDays(14),
            WithdrawalDeadline: term.TermEnd.AddDays(-30),
            GradeEntryStart:    term.TermEnd,
            GradeEntryEnd:      term.TermEnd.AddDays(14));

        Func<Task> act = async () => await svc.UpsertAsync(term.Id, dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*kayıt bitmeden başlayamaz*");
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenGradeEntryStartsBeforeAddDropEnds()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        var dto = new UpsertAcademicCalendarDto(
            RegistrationStart:  term.TermStart.AddDays(-10),
            RegistrationEnd:    term.TermStart,
            AddDropStart:       term.TermStart,
            AddDropEnd:         term.TermStart.AddDays(30),
            WithdrawalDeadline: term.TermEnd.AddDays(-30),
            GradeEntryStart:    term.TermStart.AddDays(20),     // AddDropEnd'den ÖNCE
            GradeEntryEnd:      term.TermEnd.AddDays(14));

        Func<Task> act = async () => await svc.UpsertAsync(term.Id, dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ekle-bırak bitmeden başlayamaz*");
    }

    [Fact]
    public async Task UpsertAsync_Throws_WhenGradeEntryStartsBeforeTermStart()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        var dto = new UpsertAcademicCalendarDto(
            RegistrationStart:  term.TermStart.AddDays(-30),
            RegistrationEnd:    term.TermStart.AddDays(-20),
            AddDropStart:       term.TermStart.AddDays(-15),
            AddDropEnd:         term.TermStart.AddDays(-10),
            WithdrawalDeadline: term.TermEnd.AddDays(-30),
            GradeEntryStart:    term.TermStart.AddDays(-5),     // TermStart'tan ÖNCE
            GradeEntryEnd:      term.TermEnd.AddDays(14));

        Func<Task> act = async () => await svc.UpsertAsync(term.Id, dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*dönem başlamadan başlayamaz*");
    }

    [Fact]
    public async Task UpdatePhaseOverrideAsync_SetsOverride()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));
        await svc.UpdatePhaseOverrideAsync(term.Id, new UpdateCalendarPhaseOverrideDto("Registration", true));

        var cal = ctx.TermAcademicCalendars.Single(c => c.TermId == term.Id);
        cal.RegistrationOverride.Should().Be(true);
    }

    [Fact]
    public async Task UpdatePhaseOverrideAsync_Throws_WhenTermCancelled()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));

        // Cancel the term
        term.IsCancelled = true;
        ctx.SaveChanges();

        var act = () => svc.UpdatePhaseOverrideAsync(term.Id, new UpdateCalendarPhaseOverrideDto("Registration", true));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdatePhaseOverrideAsync_Throws_ForInvalidPhase()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));

        var act = () => svc.UpdatePhaseOverrideAsync(term.Id, new UpdateCalendarPhaseOverrideDto("Bogus", true));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetStudentViewAsync_ReturnsCalendar_WithPublicEventsOnly()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));

        // Add one public and one non-public event
        ctx.TermCalendarEvents.Add(new TermCalendarEvent
        {
            TermId = term.Id,
            EventType = CalendarEventType.ClassesStart,
            Title = "Derslerin Başlangıcı",
            StartDate = term.TermStart,
            IsPublic = true,
            IsGenerated = false,
            BlocksAcademicEvents = false
        });
        ctx.TermCalendarEvents.Add(new TermCalendarEvent
        {
            TermId = term.Id,
            EventType = CalendarEventType.Other,
            Title = "Gizli Etkinlik",
            StartDate = term.TermStart.AddDays(1),
            IsPublic = false,
            IsGenerated = false,
            BlocksAcademicEvents = false
        });
        ctx.SaveChanges();

        var result = await svc.GetStudentViewAsync(term.Id);

        result.Should().NotBeNull();
        result!.Events.Should().HaveCount(1);
        result.Events[0].IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task GetStudentViewAsync_ReturnsAllEvents_WhenIncludeAllEventsRequested()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));

        ctx.TermCalendarEvents.AddRange(
            new TermCalendarEvent
            {
                TermId = term.Id,
                EventType = CalendarEventType.ClassesStart,
                Title = "Derslerin Baslangici",
                StartDate = term.TermStart,
                IsPublic = true,
                IsGenerated = false,
                BlocksAcademicEvents = false
            },
            new TermCalendarEvent
            {
                TermId = term.Id,
                EventType = CalendarEventType.Other,
                Title = "Gizli Etkinlik",
                StartDate = term.TermStart.AddDays(1),
                IsPublic = false,
                IsGenerated = false,
                BlocksAcademicEvents = false
            });
        ctx.SaveChanges();

        var result = await svc.GetStudentViewAsync(term.Id, includeAllEvents: true);

        result.Should().NotBeNull();
        result!.Events.Should().HaveCount(2);
        result.Events.Should().Contain(e => !e.IsPublic && e.Title == "Gizli Etkinlik");
    }

    [Fact]
    public async Task GetActiveStudentViewAsync_ReturnsNull_WhenNoActiveTerm()
    {
        using var ctx = CreateContext();
        // Term with far-future dates — will NOT be active for clock in the past
        var term = new AcademicTerm
        {
            Name = "2030-2031 Güz",
            ShortName = "30G",
            EnglishName = "Fall 2030",
            ShortEnglishName = "30F",
            Year = 2030,
            TermType = TermType.Fall,
            TermStart = new DateTime(2030, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            TermEnd   = new DateTime(2031, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            IsCancelled = false,
            CreatedAt = DateTime.UtcNow
        };
        ctx.AcademicTerms.Add(term);
        ctx.SaveChanges();

        // Clock set to year 2024 — term is in the future, not active
        var svc = CreateService(ctx, now: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await svc.GetActiveStudentViewAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateEventAsync_RecalculatesMidtermDependentEvents_WhenMidtermMoves()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));
        ctx.TermCalendarEvents.AddRange(
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.MidtermExam, Title = "Ara Sinavlar", StartDate = Utc(2025, 11, 10), EndDate = Utc(2025, 11, 15), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "midterm_exam" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.ExcuseExamApplication, Title = "Mazeret Sinav Basvurularinin Son Gunu", StartDate = Utc(2025, 11, 17), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "midterm_excuse_application" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.ExcuseExam, Title = "Mazeret Sinavlari", StartDate = Utc(2025, 11, 19), EndDate = Utc(2025, 11, 21), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "midterm_excuse_exam" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.MidtermGradePublication, Title = "Ara Sinavlarin Ilaninin Son Gunu", StartDate = Utc(2025, 11, 28), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "midterm_publication" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.FinalExam, Title = "Final Sinavlari", StartDate = Utc(2026, 1, 5), EndDate = Utc(2026, 1, 16), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "final_exam" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.FinalGradePublication, Title = "Final Sonuclarinin Ilani", StartDate = Utc(2026, 1, 21), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "final_publication" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.MakeupGradePublication, Title = "Butunleme Sonuclarinin Ilani", StartDate = Utc(2026, 2, 6), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "makeup_publication" });
        await ctx.SaveChangesAsync();

        var midterm = ctx.TermCalendarEvents.Single(e => e.TemplateItemKey == "midterm_exam");

        var result = await svc.UpdateEventAsync(term.Id, midterm.Id, new UpdateTermCalendarEventDto(Utc(2025, 11, 3), Utc(2025, 11, 9)));

        result.Events.Single(e => e.TemplateItemKey == "midterm_excuse_application").StartDate.Should().Be(Utc(2025, 11, 10));
        result.Events.Single(e => e.TemplateItemKey == "midterm_excuse_exam").StartDate.Should().Be(Utc(2025, 11, 12));
        result.Events.Single(e => e.TemplateItemKey == "midterm_excuse_exam").EndDate.Should().Be(Utc(2025, 11, 14));
        result.Events.Single(e => e.TemplateItemKey == "midterm_publication").StartDate.Should().Be(Utc(2025, 11, 19));
    }

    [Fact]
    public async Task UpdateEventAsync_KeepsMovedMakeupExam_AndRecalculatesDependents()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));
        ctx.TermCalendarEvents.AddRange(
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.ClassesStart, Title = "Derslerin Baslamasi", StartDate = Utc(2025, 9, 22), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "classes_start" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.ClassesEnd, Title = "Derslerin Bitisi", StartDate = Utc(2026, 1, 2), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "classes_end" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.FinalExam, Title = "Final Sinavlari", StartDate = Utc(2026, 1, 5), EndDate = Utc(2026, 1, 16), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "final_exam" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.FinalGradePublication, Title = "Final Sonuclarinin Ilani", StartDate = Utc(2026, 1, 21), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "final_publication" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.MakeupExam, Title = "Butunleme Sinavlari Basvuru Son Gunu", StartDate = Utc(2026, 1, 23), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "makeup_application" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.MakeupExam, Title = "Butunleme Sinavlari", StartDate = Utc(2026, 1, 26), EndDate = Utc(2026, 1, 30), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "makeup_exam" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.MakeupGradePublication, Title = "Butunleme Sonuclarinin Ilani", StartDate = Utc(2026, 2, 6), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "makeup_publication" });
        await ctx.SaveChangesAsync();

        var makeupExam = ctx.TermCalendarEvents.Single(e => e.TemplateItemKey == "makeup_exam");

        var result = await svc.UpdateEventAsync(term.Id, makeupExam.Id, new UpdateTermCalendarEventDto(Utc(2026, 2, 2), Utc(2026, 2, 8)));

        result.Events.Single(e => e.TemplateItemKey == "makeup_exam").StartDate.Should().Be(Utc(2026, 2, 2));
        result.Events.Single(e => e.TemplateItemKey == "makeup_exam").EndDate.Should().Be(Utc(2026, 2, 6));
        result.Events.Single(e => e.TemplateItemKey == "makeup_application").StartDate.Should().Be(Utc(2026, 1, 30));
        result.Events.Single(e => e.TemplateItemKey == "makeup_publication").StartDate.Should().Be(Utc(2026, 2, 11));
    }

    [Fact]
    public async Task UpdateEventAsync_DoesNotMoveUnrelatedPrepSchoolExam_WhenMidtermMoves()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));
        ctx.TermCalendarEvents.AddRange(
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.MidtermExam, Title = "Ara Sinavlar", StartDate = Utc(2025, 11, 10), EndDate = Utc(2025, 11, 15), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "midterm_exam" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.ExcuseExamApplication, Title = "Mazeret Sinav Basvurularinin Son Gunu", StartDate = Utc(2025, 11, 17), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "midterm_excuse_application" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.ExcuseExam, Title = "Mazeret Sinavlari", StartDate = Utc(2025, 11, 19), EndDate = Utc(2025, 11, 21), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "midterm_excuse_exam" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.MidtermGradePublication, Title = "Ara Sinavlarin Ilaninin Son Gunu", StartDate = Utc(2025, 11, 28), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "midterm_publication" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.FinalExam, Title = "Final Sinavlari", StartDate = Utc(2026, 1, 12), EndDate = Utc(2026, 1, 17), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "final_exam" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.FinalGradePublication, Title = "Final Sonuclarinin Ilani", StartDate = Utc(2026, 1, 21), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "final_publication" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.MakeupExam, Title = "Butunleme Sinavlari", StartDate = Utc(2026, 1, 26), EndDate = Utc(2026, 1, 31), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "makeup_exam" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.MakeupGradePublication, Title = "Butunleme Sonuclarinin Ilani", StartDate = Utc(2026, 2, 6), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "makeup_publication" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.PrepSchoolExam, Title = "Hazirlik (Ingilizce) Muafiyet Sinavlari", StartDate = Utc(2025, 12, 2), EndDate = Utc(2025, 12, 5), IsPublic = true, ChainCode = "PREP", TemplateItemKey = "prep_final_exam" });
        await ctx.SaveChangesAsync();

        var midterm = ctx.TermCalendarEvents.Single(e => e.TemplateItemKey == "midterm_exam");

        var result = await svc.UpdateEventAsync(term.Id, midterm.Id, new UpdateTermCalendarEventDto(Utc(2025, 11, 17), Utc(2025, 11, 23)));

        result.Events.Single(e => e.TemplateItemKey == "midterm_exam").StartDate.Should().Be(Utc(2025, 11, 17));
        result.Events.Single(e => e.TemplateItemKey == "midterm_excuse_application").StartDate.Should().Be(Utc(2025, 11, 24));
        result.Events.Single(e => e.TemplateItemKey == "prep_final_exam").StartDate.Should().Be(Utc(2025, 12, 2));
        result.Events.Single(e => e.TemplateItemKey == "prep_final_exam").EndDate.Should().Be(Utc(2025, 12, 5));
    }

    // Etkinlik taşımada da bozuk kurum saat dilimi ayarı kayıttan önce hata verir: etkinlik yerinde kalır.
    [Fact]
    public async Task UpdateEventAsync_Throws_AndSavesNothing_WhenInstitutionTimeZoneInvalid()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        await CreateService(ctx).UpsertAsync(term.Id, ValidDto(term));
        ctx.TermCalendarEvents.Add(
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.GraduateApplication, Title = "Lisansustu Program Basvurulari", StartDate = Utc(2025, 10, 1), EndDate = Utc(2025, 10, 20), IsPublic = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_graduate_app" });
        ctx.SystemConfigs.Add(new SystemConfig { Key = "Institution.TimeZone", Value = "Mars/Olympus", DataType = "string" });
        await ctx.SaveChangesAsync();
        var eventId = ctx.TermCalendarEvents.Single(e => e.TemplateItemKey == "fall_graduate_app").Id;
        var timeZone = new InstitutionTimeZoneProvider(new SystemConfigProvider(ctx, new MemoryCache(new MemoryCacheOptions())));

        await FluentActions.Awaiting(() => CreateService(ctx, timeZone: timeZone)
                .UpdateEventAsync(term.Id, eventId, new UpdateTermCalendarEventDto(Utc(2025, 10, 5), Utc(2025, 10, 24), false)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Kurum saat dilimi*");
        var stored = ctx.TermCalendarEvents.AsNoTracking().Single(e => e.Id == eventId);
        (stored.StartDate, stored.EndDate).Should().Be((Utc(2025, 10, 1), Utc(2025, 10, 20)));
    }

    [Fact]
    public async Task UpdateEventAsync_AllowsLongApplicationWindow_OnSingleDayHoliday()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));
        ctx.TermCalendarEvents.AddRange(
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.GraduateApplication, Title = "Lisansustu Program Basvurulari", StartDate = Utc(2025, 10, 1), EndDate = Utc(2025, 10, 20), IsPublic = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_graduate_app" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.NationalHoliday, Title = "Tek Gunluk Tatil", StartDate = Utc(2025, 10, 10), IsPublic = true, ChainCode = "HOLIDAY", BlocksAcademicEvents = true });
        await ctx.SaveChangesAsync();

        var graduateApplication = ctx.TermCalendarEvents.Single(e => e.TemplateItemKey == "fall_graduate_app");

        var result = await svc.UpdateEventAsync(term.Id, graduateApplication.Id, new UpdateTermCalendarEventDto(Utc(2025, 10, 5), Utc(2025, 10, 24), false));

        result.Events.Single(e => e.TemplateItemKey == "fall_graduate_app").StartDate.Should().Be(Utc(2025, 10, 5));
        result.Events.Single(e => e.TemplateItemKey == "fall_graduate_app").EndDate.Should().Be(Utc(2025, 10, 24));
    }

    [Fact]
    public async Task UpdateEventAsync_AllowsLongApplicationWindow_OnNonReligiousMultiDayHoliday()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));
        ctx.TermCalendarEvents.AddRange(
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.GraduateApplication, Title = "Lisansustu Program Basvurulari", StartDate = Utc(2025, 10, 1), EndDate = Utc(2025, 10, 20), IsPublic = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_graduate_app" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.NationalHoliday, Title = "Cumhuriyet Bayrami", StartDate = Utc(2025, 10, 28), EndDate = Utc(2025, 10, 29), IsPublic = true, ChainCode = "HOLIDAY", BlocksAcademicEvents = true });
        await ctx.SaveChangesAsync();

        var graduateApplication = ctx.TermCalendarEvents.Single(e => e.TemplateItemKey == "fall_graduate_app");

        var result = await svc.UpdateEventAsync(term.Id, graduateApplication.Id, new UpdateTermCalendarEventDto(Utc(2025, 10, 20), Utc(2025, 11, 7), false));

        result.Events.Single(e => e.TemplateItemKey == "fall_graduate_app").StartDate.Should().Be(Utc(2025, 10, 20));
        result.Events.Single(e => e.TemplateItemKey == "fall_graduate_app").EndDate.Should().Be(Utc(2025, 11, 7));
    }

    [Fact]
    public async Task UpdateEventAsync_TrimsTrailingWeekend_FromLongApplicationWindow()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));
        ctx.TermCalendarEvents.Add(
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.GraduateApplication, Title = "Lisansustu Program Basvurulari", StartDate = Utc(2025, 10, 1), EndDate = Utc(2025, 10, 20), IsPublic = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_graduate_app" });
        await ctx.SaveChangesAsync();

        var graduateApplication = ctx.TermCalendarEvents.Single(e => e.TemplateItemKey == "fall_graduate_app");

        var result = await svc.UpdateEventAsync(term.Id, graduateApplication.Id, new UpdateTermCalendarEventDto(Utc(2025, 10, 20), Utc(2025, 11, 9), false));

        result.Events.Single(e => e.TemplateItemKey == "fall_graduate_app").StartDate.Should().Be(Utc(2025, 10, 20));
        result.Events.Single(e => e.TemplateItemKey == "fall_graduate_app").EndDate.Should().Be(Utc(2025, 11, 7));
    }

    [Fact]
    public async Task UpdateEventAsync_RejectsFinalExam_OnBlockingHoliday()
    {
        using var ctx = CreateContext();
        var term = SeedTerm(ctx);
        var svc = CreateService(ctx);

        await svc.UpsertAsync(term.Id, ValidDto(term));
        ctx.TermCalendarEvents.AddRange(
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.FinalExam, Title = "Final Sinavlari", StartDate = Utc(2026, 1, 5), EndDate = Utc(2026, 1, 16), IsPublic = true, ChainCode = "MAIN", TemplateItemKey = "final_exam" },
            new TermCalendarEvent { TermId = term.Id, EventType = CalendarEventType.ImportantDay, Title = "Ataturk'u Anma Gunu", StartDate = Utc(2025, 11, 10), IsPublic = true, ChainCode = "HOLIDAY", BlocksAcademicEvents = true });
        await ctx.SaveChangesAsync();

        var finalExam = ctx.TermCalendarEvents.Single(e => e.TemplateItemKey == "final_exam");

        var act = () => svc.UpdateEventAsync(term.Id, finalExam.Id, new UpdateTermCalendarEventDto(Utc(2025, 11, 10), Utc(2025, 11, 21), false));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*taşınamaz*");
    }

    [Fact]
    public async Task ListTermsAsync_ReturnsAllTermsWithCalendarFlag()
    {
        using var ctx = CreateContext();
        var term1 = SeedTerm(ctx);
        var term2 = new AcademicTerm
        {
            Name = "2025-2026 Bahar",
            ShortName = "2025B",
            EnglishName = "2025-2026 Spring",
            ShortEnglishName = "25S",
            Year = 2025,
            TermType = TermType.Spring,
            TermStart = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc),
            TermEnd   = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            IsCancelled = false,
            CreatedAt = DateTime.UtcNow
        };
        ctx.AcademicTerms.Add(term2);
        ctx.SaveChanges();

        var svc = CreateService(ctx);

        // Only term1 gets a calendar
        await svc.UpsertAsync(term1.Id, ValidDto(term1));

        var list = await svc.ListTermsAsync();

        list.Should().HaveCount(2);
        list.Single(t => t.TermId == term1.Id).HasCalendar.Should().BeTrue();
        list.Single(t => t.TermId == term2.Id).HasCalendar.Should().BeFalse();
    }

    [Fact]
    public void AcademicCalendarNavigation_IsConfiguredAsAutoInclude()
    {
        using var ctx = CreateContext();

        var navigation = ctx.Model
            .FindEntityType(typeof(AcademicTerm))!
            .FindNavigation(nameof(AcademicTerm.AcademicCalendar))!;

        // AutoInclude → faz metotlarının ".Include unutuldu = takvim yok" tuzağına düşmesini engeller.
        navigation.IsEagerLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task AcademicTermQuery_LoadsCalendar_WithoutExplicitInclude()
    {
        var dbName = $"AutoInc-{Guid.NewGuid()}";
        DbContextOptions<AppDbContext> Options() =>
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;

        int termId;
        using (var seedCtx = new AppDbContext(Options()))
        {
            var term = SeedTerm(seedCtx);
            await CreateService(seedCtx).UpsertAsync(term.Id, ValidDto(term));
            termId = term.Id;
        }

        // Takvimi hiç track etmemiş TAZE context — navigasyonu yalnızca AutoInclude doldurabilir.
        using var queryCtx = new AppDbContext(Options());
        var loaded = await queryCtx.AcademicTerms.FirstAsync(t => t.Id == termId);

        loaded.AcademicCalendar.Should().NotBeNull();
    }
}
