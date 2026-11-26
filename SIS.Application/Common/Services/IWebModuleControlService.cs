namespace SIS.Application.Common.Services;

public static class WebModuleControlModes
{
    public const string Open = "open";
    public const string Closed = "closed";
    public const string TermEventBased = "term-event-based";
    public const string AcademicCalendarRegistration = "academic-calendar-registration";
    public const string AcademicCalendarAddDrop = "academic-calendar-add-drop";
    public const string GradeEntryPeriod = "grade-entry-period";
}

public sealed record WebModuleControlOption(
    string Code,
    string Label,
    string Description);

public sealed record WebModuleControlDefinition(
    string Code,
    string Title,
    string Category,
    string Description,
    int DisplayOrder,
    string SettingModuleKey,
    string ModeKey,
    string AnnouncementKey,
    IReadOnlyList<WebModuleControlOption> Options);

public sealed record WebModuleAccessResult(
    bool IsEnabled,
    bool IsOpen,
    string SelectedMode,
    string SelectedModeLabel,
    string EffectiveStateLabel,
    string? Message);

public sealed record WebModuleControlSummary(
    string Code,
    string Title,
    string Category,
    string Description,
    int DisplayOrder,
    int? ModeSettingId,
    int? AnnouncementSettingId,
    string SelectedMode,
    string SelectedModeLabel,
    bool IsCurrentlyOpen,
    string EffectiveStateLabel,
    string? EffectiveMessage,
    string? Announcement,
    IReadOnlyList<WebModuleControlOption> Options);

public interface IWebModuleControlService
{
    WebModuleControlDefinition? GetDefinition(string code);
    Task<IReadOnlyList<WebModuleControlSummary>> GetSummariesAsync(CancellationToken ct = default);
    Task<WebModuleAccessResult> EvaluateAsync(string code, int? termId = null, CancellationToken ct = default);
}
