using FluentAssertions;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using Xunit;

namespace SIS.Tests.Domain;

public class AcademicTermPhaseTests
{
    private static AcademicTerm BuildTerm(
        bool isCancelled = false,
        bool isActivated = true,
        DateTime? regStart = null,
        DateTime? regEnd = null,
        DateTime? addDropStart = null,
        DateTime? addDropEnd = null,
        DateTime? withdrawalDeadline = null,
        DateTime? termStart = null,
        DateTime? termEnd = null,
        DateTime? gradeStart = null,
        DateTime? gradeEnd = null,
        bool? regOverride = null,
        bool? addDropOverride = null,
        bool? withdrawalOverride = null,
        bool? gradeOverride = null)
    {
        var d = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var ts = termStart ?? d;
        var te = termEnd ?? d.AddDays(120);
        return new AcademicTerm
        {
            IsActivated = isActivated,
            IsCancelled = isCancelled,
            TermStart = ts,
            TermEnd = te,
            AcademicCalendar = new SIS.Domain.Entities.TermAcademicCalendar
            {
                RegistrationStart = regStart ?? d,
                RegistrationEnd = regEnd ?? d.AddDays(14),
                AddDropStart = addDropStart ?? d.AddDays(14),
                AddDropEnd = addDropEnd ?? d.AddDays(21),
                WithdrawalDeadline = withdrawalDeadline ?? d.AddDays(90),
                GradeEntryStart = gradeStart ?? d.AddDays(110),
                GradeEntryEnd = gradeEnd ?? d.AddDays(130),
                RegistrationOverride = regOverride,
                AddDropOverride = addDropOverride,
                WithdrawalOverride = withdrawalOverride,
                GradeEntryOverride = gradeOverride,
                CreatedAt = DateTime.UtcNow,
            }
        };
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsFalse_WhenCancelled()
    {
        var term = BuildTerm(isCancelled: true);
        // Use a time that's within the term dates — still blocked because cancelled
        var now = term.TermStart.AddDays(1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsTrue_WhenInDateRange_AndActive()
    {
        // Term is active: now is within [TermStart, TermEnd]
        var term = BuildTerm();
        var now = term.TermStart.AddDays(1); // within term dates
        term.IsRegistrationOpen(now).Should().BeTrue();
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsFalse_WhenBeforeStart()
    {
        var term = BuildTerm();
        var now = term.AcademicCalendar!.RegistrationStart.AddDays(-1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_ReturnsFalse_WhenAfterEnd()
    {
        var term = BuildTerm();
        var now = term.AcademicCalendar!.RegistrationEnd.AddDays(1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideTrue_IgnoresDate()
    {
        var term = BuildTerm(regOverride: true);
        // now is within term dates (active) but past registration window
        var now = term.TermStart.AddDays(30);
        term.IsRegistrationOpen(now).Should().BeTrue();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideFalse_IgnoresDate()
    {
        var term = BuildTerm(regOverride: false);
        var now = term.AcademicCalendar!.RegistrationStart.AddDays(1);
        term.IsRegistrationOpen(now).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideNull_RespectsDate()
    {
        var term = BuildTerm(regOverride: null);
        term.IsRegistrationOpen(term.AcademicCalendar!.RegistrationStart.AddDays(1)).Should().BeTrue();
        term.IsRegistrationOpen(term.AcademicCalendar!.RegistrationEnd.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsRegistrationOpen_OverrideTrue_StillRequiresActivated()
    {
        // Dönem "Kullanıma Açık" değil (IsActivated=false). Kayıt override'ı true olsa da,
        // aktif olmayan dönemde kayıt açılmaz.
        var term = BuildTerm(isActivated: false, regOverride: true);
        term.IsRegistrationOpen(term.TermStart.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsAddDropOpen_FollowsSameRules()
    {
        var term = BuildTerm();
        term.IsAddDropOpen(term.AcademicCalendar!.AddDropStart.AddDays(1)).Should().BeTrue();
        term.IsAddDropOpen(term.AcademicCalendar!.AddDropEnd.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsWithdrawalOpen_UsesTermStartAndWithdrawalDeadline()
    {
        var term = BuildTerm();
        term.IsWithdrawalOpen(term.TermStart.AddDays(1), TimeZoneInfo.Utc).Should().BeTrue();
        term.IsWithdrawalOpen(term.AcademicCalendar!.WithdrawalDeadline.AddDays(1), TimeZoneInfo.Utc).Should().BeFalse();
        term.IsWithdrawalOpen(term.TermStart.AddDays(-1), TimeZoneInfo.Utc).Should().BeFalse();
    }

    [Fact]
    public void IsGradeEntryOpen_UsesGradeEntryStartEnd()
    {
        var term = BuildTerm();
        term.IsGradeEntryOpen(term.AcademicCalendar!.GradeEntryStart.AddDays(1)).Should().BeTrue();
        term.IsGradeEntryOpen(term.AcademicCalendar!.GradeEntryEnd.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_TrueWhenActivatedAndNotCancelled_RegardlessOfDate()
    {
        var term = BuildTerm();
        // Aktiflik artık tarih aralığına değil, "Kullanıma Aç" bayrağına bağlı:
        // dönem tarihlerinin dışında bile aktif sayılır.
        term.IsActive(term.TermStart.AddDays(1)).Should().BeTrue();
        term.IsActive(term.TermStart.AddYears(-1)).Should().BeTrue();
        term.IsActive(term.TermEnd.AddYears(1)).Should().BeTrue();
    }

    [Fact]
    public void IsActive_FalseWhenCancelled()
    {
        var term = BuildTerm(isCancelled: true);
        term.IsActive(term.TermStart.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_FalseWhenNotActivated_EvenWithinTermDates()
    {
        var term = BuildTerm(isActivated: false);
        // Tarih aralığının içinde olsa bile: açık değilse aktif değil.
        term.IsActive(term.TermStart.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void GetStatus_ReturnsActive_WhenActivatedAndNotCancelled_RegardlessOfDate()
    {
        var term = BuildTerm();
        term.GetStatus(term.TermStart.AddDays(1)).Should().Be(TermStatus.Active);
        term.GetStatus(term.TermStart.AddYears(-1)).Should().Be(TermStatus.Active);
    }

    [Fact]
    public void GetStatus_ReturnsCancelled_WhenCancelled()
    {
        var term = BuildTerm(isCancelled: true);
        term.GetStatus(term.TermStart.AddDays(1)).Should().Be(TermStatus.Cancelled);
    }

    [Fact]
    public void GetStatus_ReturnsInactive_WhenNotActivated()
    {
        var term = BuildTerm(isActivated: false);
        term.GetStatus(term.TermStart.AddDays(1)).Should().Be(TermStatus.Inactive);
    }
}
