using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

public record SystemConfigValueOptionDto(string Value, string Label);
public record SystemConfigDto(
    int Id,
    string Key,
    string Value,
    string? Description,
    string Category,
    string DataType,
    string DisplayName,
    string GroupKey,
    string GroupLabel,
    int DisplayOrder,
    IReadOnlyList<SystemConfigValueOptionDto> Options);
public record UpdateSystemConfigDto(string Value);
public record SystemConfigImportItemDto(string Key, string Value);

[ApiController]
[Route("api/admin/system-config")]
[Authorize]
public class SystemConfigController(AppDbContext context, ISystemConfigProvider configProvider) : ControllerBase
{
    private readonly AppDbContext _context = context;
    private readonly ISystemConfigProvider _configProvider = configProvider;
    private sealed record SystemConfigPresentation(
        string DisplayName,
        string GroupKey,
        string GroupLabel,
        int DisplayOrder,
        IReadOnlyList<SystemConfigValueOptionDto>? Options = null);

    private static readonly IReadOnlyList<SystemConfigValueOptionDto> BooleanOptions =
    [
        new("false", "Hayır"),
        new("true", "Evet"),
    ];

    private static readonly IReadOnlyList<SystemConfigValueOptionDto> LanguageOptions =
    [
        new("tr-TR", "Türkçe"),
        new("en-US", "İngilizce"),
    ];

    private static readonly IReadOnlyList<SystemConfigValueOptionDto> AdvisingCalculationMethodOptions =
    [
        new("standard", "Standart"),
        new("load-based", "Yük Bazlı"),
    ];

    private static readonly IReadOnlyList<SystemConfigValueOptionDto> AttendanceMethodOptions =
    [
        new("Separate", "Teori/Uygulama Ayrı"),
        new("TotalHours", "Toplam Saate Göre"),
        new("Passive", "Pasif"),
        new("MarkOnly", "Sadece İşaretle"),
    ];

    private static readonly HashSet<string> AttendanceMethodValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "Separate", "TotalHours", "Passive", "MarkOnly",
    };

    private static readonly HashSet<string> SchedulerEngineValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "v2-ortools",
        "cp-sat",
    };

    private static readonly HashSet<string> SchedulerSearchStrategyValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto",
        "fixed",
        "portfolio",
    };

    private static readonly Dictionary<string, SystemConfigPresentation> PresentationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Application.SocialTranscriptStudentApplicationEnabled"] = new("Sosyal Transkript Öğrenci Başvuru", "application", "Başvuru", 10),
        ["Application.SocialTranscriptApplicationEvaluationEnabled"] = new("Sosyal Transkript Başvuru Değerlendirme", "application", "Başvuru", 20),
        ["Application.SocialTranscriptObjectionEnabled"] = new("Sosyal Transkript Öğrenci Başvuru İtiraz", "application", "Başvuru", 30),
        ["Application.RegistrationFreezeApplicationEnabled"] = new("Kayıt Dondurma Başvuru", "application", "Başvuru", 40),
        ["Application.CourseWithdrawalEnabled"] = new("Ders Silme Başvurusu Açık", "application", "Başvuru", 50),
        ["Instructor.StudentContactInfoVisible"] = new("Akademisyen için Öğrenci E-Posta ve GSM Bilgilerini Göster", "instructor", "Akademisyen", 10),
        ["Instructor.AcademicCvActiveYear"] = new("Akademik CV Faaliyet Girişinde Aktif Yıl", "instructor", "Akademisyen", 20),
        ["Instructor.AcademicCvApprovedActivityEditable"] = new("Akademik CV Faaliyet Girişinde Onaylı Faaliyet Değiştirilebilir", "instructor", "Akademisyen", 30),
        ["Instructor.AcademicCvAutoApprove"] = new("Akademik CV Faaliyet Girişinde Otomatik Onayla", "instructor", "Akademisyen", 40),
        ["Instructor.PhotoRequestAutoApprove"] = new("Akademik Fotoğraf Talebinde Otomatik Onayla", "instructor", "Akademisyen", 50),
        ["Instructor.SimplifyDashboardStatistics"] = new("Akademisyen açılış sayfasındaki istatistikleri sadeleştir", "instructor", "Akademisyen", 60),
        ["Instructor.ProfileOrgUnitUpdateDeadline"] = new("Akademisyen Birim, Fakülte, Bölüm, Program Girilen Tarihe Kadar Güncelleme Yapabilir", "instructor", "Akademisyen", 70),
        ["Instructor.EnableAdvisorOptOutOptions"] = new("Akademisyen Danışmanlık Yapmak İstemiyorum Seçeneklerini Aç", "instructor", "Akademisyen", 80),
        ["Instructor.DefaultLoginLanguage"] = new("Akademisyen Giriş Sayfası Varsayılan Dil", "instructor", "Akademisyen", 90, LanguageOptions),
        ["Instructor.HideGradeEntryStatistics"] = new("Akademisyen Not Girişinde İstatistik Kapalı", "instructor", "Akademisyen", 100),
        ["Instructor.RequireOfficeHoursOnProfileUpdate"] = new("Akademisyen Profil Bilgileri Girilirken Ofis Saatlerini Tanımlamaya Zorla", "instructor", "Akademisyen", 110),
        ["Instructor.FaqUrl"] = new("Akademisyen Sıkça Sorulan Sorular Bağlantı Adresi", "instructor", "Akademisyen", 120),
        ["Instructor.TranscriptClosed"] = new("Akademisyen Transkript Kapalı", "instructor", "Akademisyen", 130),
        ["Instructor.PrimaryEmailImmutable"] = new("Akademisyen 1.e-posta Adresini Değiştiremez", "instructor", "Akademisyen", 140),
        ["Instructor.AdvisingCalculationMethod"] = new("Danışmanlık Hesaplama Yöntemi", "instructor", "Akademisyen", 150, AdvisingCalculationMethodOptions),
        ["Attendance.Method"] = new("Devamsızlık Hesap Yöntemi", "attendance-policy", "Devamsızlık Politikası", 10, AttendanceMethodOptions),
        ["Attendance.TermWeekCount"] = new("Dönem Hafta Sayısı", "attendance-policy", "Devamsızlık Politikası", 20),
        ["Attendance.TheoryAbsenceLimitPercent"] = new("Teorik Devamsızlık Kalma Eşiği (%)", "attendance-policy", "Devamsızlık Politikası", 30),
        ["Attendance.LabAbsenceLimitPercent"] = new("Uygulama Devamsızlık Kalma Eşiği (%)", "attendance-policy", "Devamsızlık Politikası", 40),
        ["Registration.HideQuotaInCatalog"] = new("Ders kayıt özet sayfasında kontenjan bilgilerini gizle", "registration-controls", "Ders Kayıt Kontrolleri", 10),
        ["Registration.AllowNonCurriculumCourseSelection"] = new("Ders Kayıtta Müfredat/Bölüm Dışı Ders Listesinde Programının Dışında Ders Alabilsin mi", "registration-controls", "Ders Kayıt Kontrolleri", 20),
        ["Registration.EctsLimit.CgpaBelow200"] = new("CGPA 2.00 altı öğrenciler için AKTS üst sınırı", "registration-controls", "Ders Kayıt Kontrolleri", 30),
        ["Registration.EctsLimit.Cgpa200To249"] = new("CGPA 2.00 - 2.49 öğrenciler için AKTS üst sınırı", "registration-controls", "Ders Kayıt Kontrolleri", 40),
        ["Registration.EctsLimit.CgpaAbove250"] = new("CGPA 2.50 ve üzeri öğrenciler için AKTS üst sınırı", "registration-controls", "Ders Kayıt Kontrolleri", 50),
        ["GPA.RetentionThreshold"] = new("Sınıfta kalma için minimum AGNO eşiği", "registration-controls", "Ders Kayıt Kontrolleri", 60),
        ["Withdrawal.MaxCount.Undergraduate"] = new("Lisans öğrencisi toplam dersten çekilme hakkı", "registration-controls", "Ders Kayıt Kontrolleri", 70),
        ["Withdrawal.MaxCount.Associate"] = new("Önlisans öğrencisi toplam dersten çekilme hakkı", "registration-controls", "Ders Kayıt Kontrolleri", 80),
        ["Localization.DefaultLanguage"] = new("Sistem varsayılan dili", "general", "Genel", 20, LanguageOptions),
        [InstitutionTimeZoneValidator.ConfigKey] = new("Kurum saat dilimi (IANA)", "general", "Genel", 25),
        ["Security.PasswordRotationEnabled"] = new("Kullanıcıları periyodik şifre değişimine zorla", "general", "Genel", 30),
        ["Security.PasswordRotationMonths"] = new("Şifre değiştirme periyodu (ay)", "general", "Genel", 40),
        ["Audit.RetentionDays"] = new("Denetim kayıtlarını saklama süresi (gün)", "general", "Genel", 50),
        ["Schedule.AfternoonEndTime"] = new("Öğleden sonra son dersin en geç bitiş saati", "schedule", "Ders/Ders Programı", 10),
        ["Schedule.BreakBetweenSlotsMinutes"] = new("İki ders arası mola süresi (dakika)", "schedule", "Ders/Ders Programı", 20),
        ["Schedule.EveningSlotCount"] = new("Akşam ders slot sayısı", "schedule", "Ders/Ders Programı", 30),
        ["Schedule.LunchBreak.IsHard"] = new("Öğle arasını zorunlu kural olarak uygula", "schedule", "Ders/Ders Programı", 40),
        ["Schedule.WeekendOnlineOnly"] = new("Hafta sonu slotları sadece çevrim içi olsun", "schedule", "Ders/Ders Programı", 50),
        ["CourseOpening.DefaultCapacityPerSection"] = new("Ders açılırken / aktarımda fallback şube başı kontenjan", "course-opening", "Ders/Ders Programı", 60),
        ["CourseOpening.MaxSectionsPerCourse"] = new("Bir ders için tek seferde açılabilecek max şube sayısı", "course-opening", "Ders/Ders Programı", 70),
    };

    private static readonly Dictionary<string, (decimal? Min, decimal? Max)> NumericLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Registration.EctsLimit.CgpaBelow200"] = (0, 60),
        ["Registration.EctsLimit.Cgpa200To249"] = (0, 60),
        ["Registration.EctsLimit.CgpaAbove250"] = (0, 60),
        ["Audit.RetentionDays"] = (30, 3650),
        ["Security.PasswordRotationMonths"] = (1, 24),
        ["GPA.RetentionThreshold"] = (0, 4),
        ["Withdrawal.MaxCount.Undergraduate"] = (0, 20),
        ["Withdrawal.MaxCount.Associate"] = (0, 20),
        ["Instructor.AcademicCvActiveYear"] = (2000, 2100),
        ["Instructor.MaxWeeklyHours.Professor"] = (0, 60),
        ["Instructor.MaxWeeklyHours.AssociateProfessor"] = (0, 60),
        ["Instructor.MaxWeeklyHours.AssistantProfessor"] = (0, 60),
        ["Instructor.MaxWeeklyHours.Lecturer"] = (0, 60),
        ["Classroom.AmphiThreshold"] = (0, 500),
        ["Classroom.OccupancyTarget.Min"] = (0, 100),
        ["Classroom.OccupancyTarget.Max"] = (0, 100),
        ["SoftConstraint.S2.MaxDailyHours"] = (0, 24),
        ["SoftConstraint.S3.MaxSectionsPerSlot"] = (0, 100),
        ["SoftConstraint.S4.MinCoPairThreshold"] = (0, 1000),
        ["SoftConstraint.S7.MaxGapSlots"] = (0, 20),
        ["SoftConstraint.Weight.S1"] = (0, 10),
        ["SoftConstraint.Weight.S2"] = (0, 10),
        ["SoftConstraint.Weight.S3"] = (0, 10),
        ["SoftConstraint.Weight.S4"] = (0, 10),
        ["SoftConstraint.Weight.S5"] = (0, 10),
        ["SoftConstraint.Weight.S7"] = (0, 10),
        ["SoftConstraint.Weight.S8"] = (0, 10),
        ["SoftConstraint.Weight.S9"] = (0, 10),
        ["SoftConstraint.Weight.S12"] = (0, 10),
        ["Scheduling.MaxBacktrackingDepth"] = (0, 20),
        ["Schedule.MaxConsecutiveHours"] = (1, 12),
        ["Schedule.SlotDurationMinutes"] = (30, 180),
        ["Schedule.BreakBetweenSlotsMinutes"] = (0, 60),
        ["Schedule.EveningSlotCount"] = (0, 10),
        ["Attendance.TermWeekCount"] = (1, 52),
        ["Attendance.TheoryAbsenceLimitPercent"] = (0, 100),
        ["Attendance.LabAbsenceLimitPercent"] = (0, 100),
        ["CourseOpening.DefaultCapacityPerSection"] = (1, 1000),
        ["CourseOpening.MaxSectionsPerCourse"] = (1, 50),
    };

    private static readonly HashSet<string> TimeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Schedule.MorningStartTime",
        "Schedule.LunchBreakStart",
        "Schedule.LunchBreakEnd",
        "Schedule.EveningStartTime",
    };

    private static readonly HashSet<string> DateKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Instructor.ProfileOrgUnitUpdateDeadline",
    };

    private static readonly HashSet<string> UrlKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Instructor.FaqUrl",
    };

    private static readonly HashSet<string> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
    };

    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const NumberStyles DecimalStyle = NumberStyles.AllowLeadingWhite
        | NumberStyles.AllowTrailingWhite
        | NumberStyles.AllowLeadingSign
        | NumberStyles.AllowDecimalPoint;

    [HttpGet]
    [RequirePermission("menu.system-config.view")]
    public async Task<IActionResult> GetAll([FromQuery] string? category, CancellationToken ct)
    {
        var query = _context.SystemConfigs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category == category);

        var configs = (await query
            .ToListAsync(ct))
            .Select(MapToDto)
            .OrderBy(config => config.GroupLabel, StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: true))
            .ThenBy(config => config.DisplayOrder)
            .ThenBy(config => config.DisplayName, StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: true))
            .ToList();

        return Ok(configs);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.system-config.view")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var config = await _context.SystemConfigs.AsNoTracking()
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync(ct);

        if (config == null)
            return NotFound(new { error = "Konfigürasyon bulunamadı." });

        return Ok(MapToDto(config));
    }

    [HttpGet("limits")]
    [RequirePermission("menu.system-config.view")]
    public IActionResult GetLimits()
    {
        var limits = NumericLimits.ToDictionary(
            kvp => kvp.Key,
            kvp => new { min = kvp.Value.Min, max = kvp.Value.Max },
            StringComparer.OrdinalIgnoreCase);

        return Ok(limits);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.system-config.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSystemConfigDto dto, CancellationToken ct)
    {
        var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (config == null)
            return NotFound(new { error = "Konfigürasyon bulunamadı." });

        if (!TryNormalizeAndValidateValue(config.Key, config.DataType, dto.Value, out var normalizedValue, out var error))
            return BadRequest(new { error });

        config.Value = normalizedValue;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Bu ayar başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin." });
        }

        _configProvider.InvalidateCache(config.Key);

        return Ok(MapToDto(config));
    }

    [HttpGet("export")]
    [RequirePermission("menu.system-config.export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var configs = (await _context.SystemConfigs
            .AsNoTracking()
            .ToListAsync(ct))
            .Select(MapToDto)
            .OrderBy(config => config.GroupLabel, StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: true))
            .ThenBy(config => config.DisplayOrder)
            .ThenBy(config => config.DisplayName, StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: true))
            .ToList();

        var json = JsonSerializer.Serialize(configs, ExportJsonOptions);
        var fileName = $"system-config-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        return File(Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    private static SystemConfigDto MapToDto(SIS.Domain.Entities.SystemConfig config)
    {
        var presentation = ResolvePresentation(config);
        var options = presentation.Options
            ?? (string.Equals(config.DataType, "bool", StringComparison.OrdinalIgnoreCase)
                ? BooleanOptions
                : []);

        return new SystemConfigDto(
            config.Id,
            config.Key,
            config.Value,
            config.Description,
            config.Category,
            config.DataType,
            presentation.DisplayName,
            presentation.GroupKey,
            presentation.GroupLabel,
            presentation.DisplayOrder,
            options);
    }

    private static SystemConfigPresentation ResolvePresentation(SIS.Domain.Entities.SystemConfig config)
    {
        if (PresentationMap.TryGetValue(config.Key, out var presentation))
            return presentation;

        var (groupKey, groupLabel, baseOrder) = ResolveGroup(config.Category);
        return new SystemConfigPresentation(
            config.Description ?? config.Key,
            groupKey,
            groupLabel,
            baseOrder + Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(config.Key)) % 10_000);
    }

    private static (string GroupKey, string GroupLabel, int BaseOrder) ResolveGroup(string category)
    {
        return category.Trim().ToLowerInvariant() switch
        {
            "instructor" => ("instructor", "Akademisyen", 10_000),
            "application" => ("application", "Başvuru", 20_000),
            "registrationcontrol" => ("registration-controls", "Ders Kayıt Kontrolleri", 30_000),
            "registration" => ("registration-controls", "Ders Kayıt Kontrolleri", 30_000),
            "gpa" => ("registration-controls", "Ders Kayıt Kontrolleri", 30_000),
            "schedule" => ("schedule", "Ders/Ders Programı", 40_000),
            "scheduling" => ("schedule", "Ders/Ders Programı", 40_000),
            "courseopening" => ("course-opening", "Ders/Ders Programı", 40_000),
            "attendance" => ("general", "Genel", 50_000),
            "security" => ("general", "Genel", 50_000),
            "localization" => ("general", "Genel", 50_000),
            "general" => ("general", "Genel", 50_000),
            _ => ("general", "Genel", 90_000),
        };
    }

    [HttpPost("import")]
    [RequirePermission("menu.system-config.import")]
    public async Task<IActionResult> Import([FromBody] List<SystemConfigImportItemDto>? items, CancellationToken ct)
    {
        if (items is null || items.Count == 0)
            return BadRequest(new { error = "İçe aktarım için en az bir ayar gönderilmelidir." });

        if (items.Any(item => string.IsNullOrWhiteSpace(item.Key)))
            return BadRequest(new { error = "Tüm ayar kayıtlarında geçerli bir anahtar bulunmalıdır." });

        var duplicateKeys = items
            .GroupBy(item => item.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key)
            .ToList();

        if (duplicateKeys.Count > 0)
            return BadRequest(new { error = $"İçe aktarılan JSON içinde tekrar eden anahtarlar var: {string.Join(", ", duplicateKeys)}" });

        var requestedKeys = items
            .Select(item => item.Key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var configs = await _context.SystemConfigs
            .Where(config => requestedKeys.Contains(config.Key))
            .ToListAsync(ct);

        var configByKey = configs.ToDictionary(config => config.Key, StringComparer.OrdinalIgnoreCase);
        var missingKeys = requestedKeys
            .Where(key => !configByKey.ContainsKey(key))
            .OrderBy(key => key)
            .ToList();

        if (missingKeys.Count > 0)
            return BadRequest(new { error = $"Bilinmeyen konfigürasyon anahtarları bulundu: {string.Join(", ", missingKeys)}" });

        var updates = new List<(SIS.Domain.Entities.SystemConfig Config, string NormalizedValue)>();
        foreach (var item in items)
        {
            var key = item.Key.Trim();
            var config = configByKey[key];

            if (!TryNormalizeAndValidateValue(config.Key, config.DataType, item.Value, out var normalizedValue, out var error))
                return BadRequest(new { error = $"{config.Key}: {error}" });

            updates.Add((config, normalizedValue));
        }

        var updatedKeys = new List<string>();
        foreach (var update in updates)
        {
            if (string.Equals(update.Config.Value, update.NormalizedValue, StringComparison.Ordinal))
                continue;

            update.Config.Value = update.NormalizedValue;
            updatedKeys.Add(update.Config.Key);
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Bir veya daha fazla ayar başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin." });
        }

        foreach (var key in updatedKeys)
            _configProvider.InvalidateCache(key);

        return Ok(new
        {
            updatedCount = updatedKeys.Count,
            processedCount = items.Count,
        });
    }

    private static bool TryNormalizeAndValidateValue(
        string key,
        string dataType,
        string? rawValue,
        out string normalizedValue,
        out string error)
    {
        normalizedValue = string.Empty;
        error = string.Empty;

        if (rawValue is null)
        {
            error = "Değer boş olamaz.";
            return false;
        }

        switch (dataType.Trim().ToLowerInvariant())
        {
            case "int":
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    error = "Geçerli bir tamsayı girilmelidir.";
                    return false;
                }

                if (!TryValidateNumericLimit(key, intValue, out error))
                    return false;

                normalizedValue = intValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case "decimal":
                if (!decimal.TryParse(rawValue, DecimalStyle, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    error = "Ondalık değerler nokta (.) ile ve geçerli sayısal formatta girilmelidir.";
                    return false;
                }

                if (!TryValidateNumericLimit(key, decimalValue, out error))
                    return false;

                normalizedValue = decimalValue.ToString(CultureInfo.InvariantCulture);
                return true;

            case "bool":
                if (!TryParseBoolean(rawValue, out var boolValue))
                {
                    error = "Mantıksal değer olarak true/false, 1/0, yes/no veya on/off girilmelidir.";
                    return false;
                }

                normalizedValue = boolValue ? "true" : "false";
                return true;

            case "string":
                if (rawValue.Length > 500)
                {
                    error = "Metin değerleri en fazla 500 karakter olabilir.";
                    return false;
                }

                if (string.Equals(key, InstitutionTimeZoneValidator.ConfigKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (!InstitutionTimeZoneValidator.TryResolve(rawValue.Trim(), out var timeZone, out error))
                        return false;

                    normalizedValue = timeZone.Id;
                    return true;
                }

                if (DateKeys.Contains(key))
                {
                    if (!DateOnly.TryParseExact(rawValue.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                    {
                        error = "Tarih değeri yyyy-MM-dd formatında girilmelidir (ör. 2026-05-05).";
                        return false;
                    }

                    normalizedValue = dateValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    return true;
                }

                if (TimeKeys.Contains(key))
                {
                    if (!TimeOnly.TryParseExact(rawValue.Trim(), "HH:mm", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    {
                        error = "Saat değeri HH:mm formatında girilmelidir (ör. 09:00, 13:30).";
                        return false;
                    }

                    normalizedValue = rawValue.Trim();
                    return true;
                }

                if (string.Equals(key, "Schedule.WorkDays", StringComparison.OrdinalIgnoreCase))
                {
                    var days = rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (days.Length == 0)
                    {
                        error = "En az bir çalışma günü belirtilmelidir.";
                        return false;
                    }

                    var invalidDays = days.Where(d => !DayNames.Contains(d)).ToList();
                    if (invalidDays.Count > 0)
                    {
                        error = $"Geçersiz gün adları: {string.Join(", ", invalidDays)}. Geçerli değerler: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday";
                        return false;
                    }

                    normalizedValue = string.Join(",", days);
                    return true;
                }

                if (string.Equals(key, "Attendance.Method", StringComparison.OrdinalIgnoreCase))
                {
                    var trimmedValue = rawValue.Trim();
                    if (!AttendanceMethodValues.Contains(trimmedValue))
                    {
                        error = "Geçerli devamsızlık yöntemi değerleri: Separate, TotalHours, Passive, MarkOnly";
                        return false;
                    }

                    // Normalise to the canonical casing stored in the option list
                    var canonical = AttendanceMethodOptions.FirstOrDefault(o =>
                        string.Equals(o.Value, trimmedValue, StringComparison.OrdinalIgnoreCase))?.Value
                        ?? trimmedValue;
                    normalizedValue = canonical;
                    return true;
                }

                if (string.Equals(key, "Schedule.Engine", StringComparison.OrdinalIgnoreCase))
                {
                    var trimmedValue = rawValue.Trim();
                    if (!SchedulerEngineValues.Contains(trimmedValue))
                    {
                        error = "Geçerli motor değerleri: v2-ortools, cp-sat";
                        return false;
                    }

                    normalizedValue = trimmedValue.ToLowerInvariant();
                    return true;
                }

                if (string.Equals(key, "Schedule.OrTools.SearchStrategy", StringComparison.OrdinalIgnoreCase))
                {
                    var trimmedValue = rawValue.Trim();
                    if (!SchedulerSearchStrategyValues.Contains(trimmedValue))
                    {
                        error = "Geçerli arama stratejileri: auto, fixed, portfolio";
                        return false;
                    }

                    normalizedValue = trimmedValue.ToLowerInvariant();
                    return true;
                }

                if (UrlKeys.Contains(key))
                {
                    if (!Uri.TryCreate(rawValue.Trim(), UriKind.Absolute, out var uri)
                        || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        error = "Geçerli bir http/https URL değeri girilmelidir.";
                        return false;
                    }

                    normalizedValue = uri.ToString();
                    return true;
                }

                normalizedValue = rawValue;
                return true;

            case "json":
                try
                {
                    JsonDocument.Parse(rawValue);
                    normalizedValue = rawValue;
                    return true;
                }
                catch (JsonException)
                {
                    error = "Geçerli bir JSON değeri girilmelidir.";
                    return false;
                }

            default:
                error = $"Desteklenmeyen veri tipi: {dataType}";
                return false;
        }
    }

    private static bool TryValidateNumericLimit(string key, decimal value, out string error)
    {
        error = string.Empty;

        if (!NumericLimits.TryGetValue(key, out var limit))
            return true;

        if (limit.Min.HasValue && value < limit.Min.Value)
        {
            error = $"Değer en az {limit.Min.Value.ToString(CultureInfo.InvariantCulture)} olmalıdır.";
            return false;
        }

        if (limit.Max.HasValue && value > limit.Max.Value)
        {
            error = $"Değer en fazla {limit.Max.Value.ToString(CultureInfo.InvariantCulture)} olabilir.";
            return false;
        }

        return true;
    }

    private static bool TryParseBoolean(string? value, out bool result)
    {
        result = false;

        var normalized = value?.Trim().ToLowerInvariant();
        if (normalized is null)
            return false;

        if (normalized is "true" or "1" or "yes" or "on")
        {
            result = true;
            return true;
        }

        if (normalized is "false" or "0" or "no" or "off")
        {
            result = false;
            return true;
        }

        return false;
    }
}
