using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Tests;
using SIS.Tests.TestDoubles;

namespace SIS.Tests.Application;

public class WebModuleControlServiceTests
{
    [Fact]
    public async Task EvaluateAsync_WhenModuleIsClosed_IgnoresStaleAnnouncementAndReturnsClosedMessage()
    {
        await using var context = CreateContext();
        context.WebModuleSettings.AddRange(
            new WebModuleSetting
            {
                ModuleKey = "MidtermGradeEntry",
                Key = "Mode",
                Value = WebModuleControlModes.Closed,
            },
            new WebModuleSetting
            {
                ModuleKey = "MidtermGradeEntry",
                Key = "Announcement",
                Value = "Ara sınav not girişi yalnızca ilgili takvim penceresinde açıktır.",
            });
        await context.SaveChangesAsync();

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var provider = new WebModuleSettingsProvider(context, memoryCache, new FakeClock());
        var service = new WebModuleControlService(context, provider, new FakeClock(), new SabitKurumSaatDilimi("Etc/UTC"), NullLogger<WebModuleControlService>.Instance);

        var result = await service.EvaluateAsync("midterm-grade-entry");

        result.IsOpen.Should().BeFalse();
        result.EffectiveStateLabel.Should().Be("Kapalı");
        result.Message.Should().Be("Ara Sınav Not Giriş modülü kapatılmıştır.");
    }

    [Fact]
    public async Task EvaluateAsync_WhenRegistrationModuleIsManuallyOpen_DoesNotWaitForDailyOpeningHour()
    {
        await using var context = CreateContext();
        context.WebModuleSettings.AddRange(
            new WebModuleSetting
            {
                ModuleKey = "StudentCourseRegistration",
                Key = "Mode",
                Value = WebModuleControlModes.Open,
            },
            new WebModuleSetting
            {
                ModuleKey = "StudentPanel",
                Key = "CourseRegistrationOpenAt",
                Value = "09:00",
            });
        await context.SaveChangesAsync();

        var clock = new FakeClock { UtcNow = new DateTime(2026, 5, 21, 5, 0, 0, DateTimeKind.Utc) };
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var provider = new WebModuleSettingsProvider(context, memoryCache, clock);
        var service = new WebModuleControlService(context, provider, clock, new SabitKurumSaatDilimi("Etc/UTC"), NullLogger<WebModuleControlService>.Instance);

        var result = await service.EvaluateAsync("student-course-registration");

        result.IsOpen.Should().BeTrue();
        result.EffectiveStateLabel.Should().Be("Açık");
        result.Message.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WhenRegistrationModuleIsCalendarBound_WaitsForDailyOpeningHour()
    {
        await using var context = CreateContext();
        var term = new AcademicTerm
        {
            Name = "2025-2026 Bahar",
            ShortName = "2025 Bahar",
            EnglishName = "2025-2026 Spring",
            ShortEnglishName = "2025 Spring",
            Year = 2026,
            TermType = TermType.Spring,
            IsActivated = true,
            TermStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            CalendarEvents =
            {
                new TermCalendarEvent
                {
                    EventType = CalendarEventType.CourseRegistration,
                    Title = "Ders Kayıt",
                    StartDate = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2026, 5, 25, 23, 59, 59, DateTimeKind.Utc),
                },
            },
        };

        context.AcademicTerms.Add(term);
        context.WebModuleSettings.AddRange(
            new WebModuleSetting
            {
                ModuleKey = "StudentCourseRegistration",
                Key = "Mode",
                Value = WebModuleControlModes.TermEventBased,
            },
            new WebModuleSetting
            {
                ModuleKey = "StudentPanel",
                Key = "CourseRegistrationOpenAt",
                Value = "09:00",
            });
        await context.SaveChangesAsync();

        var clock = new FakeClock { UtcNow = new DateTime(2026, 5, 21, 5, 0, 0, DateTimeKind.Utc) };
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var provider = new WebModuleSettingsProvider(context, memoryCache, clock);
        var service = new WebModuleControlService(context, provider, clock, new SabitKurumSaatDilimi("Etc/UTC"), NullLogger<WebModuleControlService>.Instance);

        var result = await service.EvaluateAsync("student-course-registration");

        result.IsOpen.Should().BeFalse();
        result.EffectiveStateLabel.Should().Be("Saat Bekliyor");
        result.Message.Should().Be("Ders kayıt işlemleri bugün saat 09:00 itibarıyla açılacaktır.");
    }

    [Fact]
    public async Task EvaluateAsync_WhenAdvisorRegistrationApprovalUsesCourseRegistrationEvent_ReturnsOpen()
    {
        await using var context = CreateContext();
        context.AcademicTerms.Add(new AcademicTerm
        {
            Name = "2025-2026 Bahar",
            Year = 2026,
            TermType = TermType.Spring,
            TermStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            CalendarEvents =
            {
                new TermCalendarEvent
                {
                    EventType = CalendarEventType.CourseRegistration,
                    Title = "Ders Kayit ve Danisman Onaylari",
                    StartDate = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2026, 5, 25, 23, 59, 59, DateTimeKind.Utc),
                },
            },
            IsActivated = true,
        });
        context.WebModuleSettings.Add(new WebModuleSetting
        {
            ModuleKey = "AdvisorRegistrationApproval",
            Key = "Mode",
            Value = WebModuleControlModes.TermEventBased,
        });
        await context.SaveChangesAsync();

        var clock = new FakeClock { UtcNow = new DateTime(2026, 5, 21, 10, 0, 0, DateTimeKind.Utc) };
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var provider = new WebModuleSettingsProvider(context, memoryCache, clock);
        var service = new WebModuleControlService(context, provider, clock, new SabitKurumSaatDilimi("Etc/UTC"), NullLogger<WebModuleControlService>.Instance);

        var result = await service.EvaluateAsync("advisor-registration-approval");

        result.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WhenAdvisorAddDropApprovalUsesAddDropEvent_ReturnsOpen()
    {
        await using var context = CreateContext();
        context.AcademicTerms.Add(new AcademicTerm
        {
            Name = "2025-2026 Bahar",
            Year = 2026,
            TermType = TermType.Spring,
            TermStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            TermEnd = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc),
            CalendarEvents =
            {
                new TermCalendarEvent
                {
                    EventType = CalendarEventType.AddDrop,
                    Title = "Ders Ekleme - Birakma ve Danisman Onaylari",
                    StartDate = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2026, 5, 25, 23, 59, 59, DateTimeKind.Utc),
                },
            },
            IsActivated = true,
        });
        context.WebModuleSettings.Add(new WebModuleSetting
        {
            ModuleKey = "AdvisorAddDropApproval",
            Key = "Mode",
            Value = WebModuleControlModes.TermEventBased,
        });
        await context.SaveChangesAsync();

        var clock = new FakeClock { UtcNow = new DateTime(2026, 5, 21, 10, 0, 0, DateTimeKind.Utc) };
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var provider = new WebModuleSettingsProvider(context, memoryCache, clock);
        var service = new WebModuleControlService(context, provider, clock, new SabitKurumSaatDilimi("Etc/UTC"), NullLogger<WebModuleControlService>.Instance);

        var result = await service.EvaluateAsync("advisor-add-drop-approval");

        result.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WhenRegistrationModuleHasNoConfig_DoesNotFailOpen()
    {
        // Hiç WebModuleSetting yok → fallback "Açık" OLMAMALI; takvime bağlı davranır,
        // aktif dönem/takvim olmadığı için kapalı döner (fail-closed).
        await using var context = CreateContext();

        var clock = new FakeClock { UtcNow = new DateTime(2026, 5, 21, 5, 0, 0, DateTimeKind.Utc) };
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var provider = new WebModuleSettingsProvider(context, memoryCache, clock);
        var service = new WebModuleControlService(context, provider, clock, new SabitKurumSaatDilimi("Etc/UTC"), NullLogger<WebModuleControlService>.Instance);

        var result = await service.EvaluateAsync("student-course-registration");

        result.IsOpen.Should().BeFalse();
        result.SelectedMode.Should().Be(WebModuleControlModes.TermEventBased);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
