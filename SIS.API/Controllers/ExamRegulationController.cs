using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Authorization;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/admin/exam-regulations")]
[Authorize]
public class ExamRegulationController(AppDbContext context, ISystemConfigProvider config) : ControllerBase
{
    private readonly ISystemConfigProvider _config = config;

    [HttpGet]
    [RequirePermission("menu.sinav-mufredati.view")]
    public async Task<IActionResult> List([FromQuery] int? programId, CancellationToken ct)
    {
        var query = context.ExamRegulations.AsNoTracking().Include(r => r.Program);
        var filtered = programId.HasValue
            ? query.Where(r => r.ProgramId == programId.Value)
            : query;

        var entities = await filtered
            .OrderByDescending(r => r.Year).ThenBy(r => r.Program.Name)
            .ToListAsync(ct);
        var items = entities.Select(r => ToDto(r)).ToList();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("menu.sinav-mufredati.view")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var entity = await context.ExamRegulations.AsNoTracking()
            .Include(r => r.Program)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound(new { message = "Sınav yönergesi bulunamadı." });
        return Ok(ToDto(entity));
    }

    [HttpPost]
    [RequirePermission("menu.sinav-mufredati.create")]
    public async Task<IActionResult> Create([FromBody] SaveExamRegulationRequest req, CancellationToken ct)
    {
        var lockResult = await EnsureNotGloballyLockedAsync(ct);
        if (lockResult != null) return lockResult;

        var validation = Validate(req);
        if (validation != null) return BadRequest(new { message = validation });

        if (await ScopeExistsAsync(req.ProgramId, req.DegreeLevel, req.TermType, req.Year, null, ct))
            return BadRequest(new { message = "Bu program/öğrenci tipi/dönem/yıl için zaten bir yönerge var." });

        var entity = new ExamRegulation { CreatedAt = DateTime.UtcNow };
        Apply(entity, req);
        context.ExamRegulations.Add(entity);
        await context.SaveChangesAsync(ct);

        await context.Entry(entity).Reference(e => e.Program).LoadAsync(ct);
        return Ok(ToDto(entity));
    }

    [HttpPut("{id:int}")]
    [RequirePermission("menu.sinav-mufredati.edit")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveExamRegulationRequest req, CancellationToken ct)
    {
        var lockResult = await EnsureNotGloballyLockedAsync(ct);
        if (lockResult != null) return lockResult;

        var entity = await context.ExamRegulations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound(new { message = "Sınav yönergesi bulunamadı." });

        var validation = Validate(req);
        if (validation != null) return BadRequest(new { message = validation });

        if (await ScopeExistsAsync(req.ProgramId, req.DegreeLevel, req.TermType, req.Year, id, ct))
            return BadRequest(new { message = "Bu program/öğrenci tipi/dönem/yıl için zaten başka bir yönerge var." });

        Apply(entity, req);
        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        await context.Entry(entity).Reference(e => e.Program).LoadAsync(ct);
        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("menu.sinav-mufredati.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var lockResult = await EnsureNotGloballyLockedAsync(ct);
        if (lockResult != null) return lockResult;

        var entity = await context.ExamRegulations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null) return NotFound(new { message = "Sınav yönergesi bulunamadı." });
        context.ExamRegulations.Remove(entity);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("attendance-policy")]
    [RequirePermission("menu.sinav-mufredati.view")]
    public async Task<IActionResult> GetAttendancePolicy(CancellationToken ct)
    {
        var weekCount = await _config.GetIntAsync("Attendance.TermWeekCount", 14, ct);
        var theoryPercent = await _config.GetDecimalAsync("Attendance.TheoryAbsenceLimitPercent", 30, ct);
        var labPercent = await _config.GetDecimalAsync("Attendance.LabAbsenceLimitPercent", 20, ct);
        var methodStr = await _config.GetStringAsync("Attendance.Method", "Separate", ct);
        return Ok(new AttendancePolicyDto(weekCount, theoryPercent, labPercent, methodStr));
    }

    [HttpPost("{id:int}/clone")]
    [RequirePermission("menu.sinav-mufredati.create")]
    public async Task<IActionResult> Clone(int id, [FromBody] CloneExamRegulationRequest req, CancellationToken ct)
    {
        var lockResult = await EnsureNotGloballyLockedAsync(ct);
        if (lockResult != null) return lockResult;

        var source = await context.ExamRegulations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (source == null) return NotFound(new { message = "Kaynak yönerge bulunamadı." });

        if (req.Year < 2000 || req.Year > 2100)
            return BadRequest(new { message = "Yıl geçerli değil." });

        var targetProgramId = req.ProgramId ?? source.ProgramId;
        if (await ScopeExistsAsync(targetProgramId, source.DegreeLevel, source.TermType, req.Year, null, ct))
            return BadRequest(new { message = "Hedef program/dönem/yıl için zaten bir yönerge var." });

        var clone = new ExamRegulation
        {
            ProgramId = targetProgramId,
            DegreeLevel = source.DegreeLevel,
            TermType = source.TermType,
            Year = req.Year,
            IsActive = source.IsActive,
            Name = source.Name,
            MidtermCount = source.MidtermCount,
            FinalCount = source.FinalCount,
            MinMidtermWeightPercent = source.MinMidtermWeightPercent,
            MaxMidtermWeightPercent = source.MaxMidtermWeightPercent,
            MinFinalWeightPercent = source.MinFinalWeightPercent,
            MaxFinalWeightPercent = source.MaxFinalWeightPercent,
            PassingGrade = source.PassingGrade,
            RawScoreFloor1 = source.RawScoreFloor1,
            RawScoreFloor2 = source.RawScoreFloor2,
            FinalExamFloor = source.FinalExamFloor,
            AAMin = source.AAMin,
            BAMin = source.BAMin,
            BBMin = source.BBMin,
            CBMin = source.CBMin,
            CCMin = source.CCMin,
            DCMin = source.DCMin,
            DDMin = source.DDMin,
            LetterRangesEditable = source.LetterRangesEditable,
            BelowFloorGrade = source.BelowFloorGrade,
            EvaluationType = source.EvaluationType,
            RelativeMinStudentCount = source.RelativeMinStudentCount,
            RelativeInclusionLimit = source.RelativeInclusionLimit,
            RelativeAbsoluteFloor = source.RelativeAbsoluteFloor,
            TAAMin = source.TAAMin,
            TBAMin = source.TBAMin,
            TBBMin = source.TBBMin,
            TCBMin = source.TCBMin,
            TCCMin = source.TCCMin,
            TDCMin = source.TDCMin,
            TDDMin = source.TDDMin,
            BlockFinalIfNoMidterm = source.BlockFinalIfNoMidterm,
            FailIfNoFinal = source.FailIfNoFinal,
            BlockMakeupIfNoFinal = source.BlockMakeupIfNoFinal,
            ZeroIfMissedMakeup = source.ZeroIfMissedMakeup,
            MakeupReplacesFinal = source.MakeupReplacesFinal,
            AutoTransferFailedToMakeup = source.AutoTransferFailedToMakeup,
            AutoTransferExcusedToMakeup = source.AutoTransferExcusedToMakeup,
            CreatedAt = DateTime.UtcNow
        };
        context.ExamRegulations.Add(clone);
        await context.SaveChangesAsync(ct);
        await context.Entry(clone).Reference(e => e.Program).LoadAsync(ct);
        return Ok(ToDto(clone));
    }

    private async Task<bool> ScopeExistsAsync(int programId, DegreeLevel level, TermType termType, int year, int? excludeId, CancellationToken ct) =>
        await context.ExamRegulations.AsNoTracking().AnyAsync(r =>
            r.ProgramId == programId && r.DegreeLevel == level &&
            r.TermType == termType && r.Year == year &&
            (excludeId == null || r.Id != excludeId.Value), ct);

    private async Task<IActionResult?> EnsureNotGloballyLockedAsync(CancellationToken ct)
    {
        var locked = await _config.GetBoolAsync("ExamRegulation.GlobalEditLock", false, ct);
        if (locked && !User.IsInRole("SystemAdministrator"))
            return new ObjectResult(new { message = "Sınav yönergeleri şu an genel olarak düzenlemeye kapalı." }) { StatusCode = StatusCodes.Status403Forbidden };
        return null;
    }

    private static string? Validate(SaveExamRegulationRequest req)
    {
        if (req.ProgramId <= 0) return "Program seçilmelidir.";
        if (string.IsNullOrWhiteSpace(req.Name)) return "Yönerge adı boş olamaz.";
        if (req.Year < 2000 || req.Year > 2100) return "Yıl geçerli değil.";
        if (req.MidtermCount < 0 || req.FinalCount < 0) return "Sınav sayısı negatif olamaz.";
        foreach (var (min, max, label) in new[]
                 {
                     (req.MinMidtermWeightPercent, req.MaxMidtermWeightPercent, "Ara sınav etki oranı"),
                     (req.MinFinalWeightPercent, req.MaxFinalWeightPercent, "YYS etki oranı")
                 })
        {
            if (min < 0 || max > 100) return $"{label} 0-100 arasında olmalıdır.";
            if (min > max) return $"{label}: en az değer en çok değeri aşamaz.";
        }
        if (req.PassingGrade is null) return "Geçme notu gönderilmelidir.";
        if (req.PassingGrade < 0 || req.PassingGrade > 100) return "Geçme notu 0-100 arasında olmalıdır.";
        foreach (var (value, label) in new[]
                 {
                     (req.RawScoreFloor1, "Ham başarı notu alt limiti"),
                     (req.RawScoreFloor2, "Ham başarı notu alt limiti 2"),
                     (req.FinalExamFloor, "Yarıyıl sonu sınav limiti")
                 })
        {
            if (value < 0 || value > 100) return $"{label} 0-100 arasında olmalıdır.";
        }
        if (!(req.AAMin > req.BAMin && req.BAMin > req.BBMin && req.BBMin > req.CBMin &&
              req.CBMin > req.CCMin && req.CCMin > req.DCMin && req.DCMin > req.DDMin && req.DDMin > 0))
            return "Harf aralıkları büyükten küçüğe sıralı ve pozitif olmalıdır.";
        if (req.EvaluationType == GradeEvaluationType.Relative &&
            !(req.TAAMin > req.TBAMin && req.TBAMin > req.TBBMin && req.TBBMin > req.TCBMin &&
              req.TCBMin > req.TCCMin && req.TCCMin > req.TDCMin && req.TDCMin > req.TDDMin && req.TDDMin > 0))
            return "T-skor eşikleri büyükten küçüğe sıralı ve pozitif olmalıdır.";
        return null;
    }

    private static void Apply(ExamRegulation e, SaveExamRegulationRequest req)
    {
        e.ProgramId = req.ProgramId;
        e.DegreeLevel = req.DegreeLevel;
        e.TermType = req.TermType;
        e.Year = req.Year;
        e.IsActive = req.IsActive;
        e.Name = req.Name.Trim();
        e.MidtermCount = req.MidtermCount;
        e.FinalCount = req.FinalCount;
        e.MinMidtermWeightPercent = req.MinMidtermWeightPercent;
        e.MaxMidtermWeightPercent = req.MaxMidtermWeightPercent;
        e.MinFinalWeightPercent = req.MinFinalWeightPercent;
        e.MaxFinalWeightPercent = req.MaxFinalWeightPercent;
        e.PassingGrade = req.PassingGrade ?? throw new InvalidOperationException("Geçme notu doğrulanmadan yazılamaz.");
        e.RawScoreFloor1 = req.RawScoreFloor1;
        e.RawScoreFloor2 = req.RawScoreFloor2;
        e.FinalExamFloor = req.FinalExamFloor;
        e.AAMin = req.AAMin;
        e.BAMin = req.BAMin;
        e.BBMin = req.BBMin;
        e.CBMin = req.CBMin;
        e.CCMin = req.CCMin;
        e.DCMin = req.DCMin;
        e.DDMin = req.DDMin;
        e.LetterRangesEditable = req.LetterRangesEditable;
        e.BelowFloorGrade = req.BelowFloorGrade;
        e.EvaluationType = req.EvaluationType;
        e.RelativeMinStudentCount = req.RelativeMinStudentCount;
        e.RelativeInclusionLimit = req.RelativeInclusionLimit;
        e.RelativeAbsoluteFloor = req.RelativeAbsoluteFloor;
        e.TAAMin = req.TAAMin;
        e.TBAMin = req.TBAMin;
        e.TBBMin = req.TBBMin;
        e.TCBMin = req.TCBMin;
        e.TCCMin = req.TCCMin;
        e.TDCMin = req.TDCMin;
        e.TDDMin = req.TDDMin;
        e.BlockFinalIfNoMidterm = req.BlockFinalIfNoMidterm;
        e.FailIfNoFinal = req.FailIfNoFinal;
        e.BlockMakeupIfNoFinal = req.BlockMakeupIfNoFinal;
        e.ZeroIfMissedMakeup = req.ZeroIfMissedMakeup;
        e.MakeupReplacesFinal = req.MakeupReplacesFinal;
        e.AutoTransferFailedToMakeup = req.AutoTransferFailedToMakeup;
        e.AutoTransferExcusedToMakeup = req.AutoTransferExcusedToMakeup;
    }

    private static ExamRegulationDto ToDto(ExamRegulation r) => new(
        r.Id, r.ProgramId, r.Program?.Name ?? "", r.DegreeLevel.ToString(), r.TermType.ToString(),
        r.Year, r.IsActive, r.Name, r.MidtermCount, r.FinalCount,
        r.MinMidtermWeightPercent, r.MaxMidtermWeightPercent,
        r.MinFinalWeightPercent, r.MaxFinalWeightPercent,
        r.PassingGrade, r.RawScoreFloor1, r.RawScoreFloor2, r.FinalExamFloor,
        r.AAMin, r.BAMin, r.BBMin, r.CBMin, r.CCMin, r.DCMin, r.DDMin, r.LetterRangesEditable,
        r.BelowFloorGrade.ToString(),
        r.EvaluationType.ToString(),
        r.RelativeMinStudentCount, r.RelativeInclusionLimit, r.RelativeAbsoluteFloor,
        r.TAAMin, r.TBAMin, r.TBBMin, r.TCBMin, r.TCCMin, r.TDCMin, r.TDDMin,
        r.BlockFinalIfNoMidterm, r.FailIfNoFinal, r.BlockMakeupIfNoFinal,
        r.ZeroIfMissedMakeup, r.MakeupReplacesFinal,
        r.AutoTransferFailedToMakeup, r.AutoTransferExcusedToMakeup);
}

public record ExamRegulationDto(
    int Id, int ProgramId, string ProgramName, string DegreeLevel, string TermType,
    int Year, bool IsActive, string Name, int MidtermCount, int FinalCount,
    decimal MinMidtermWeightPercent, decimal MaxMidtermWeightPercent,
    decimal MinFinalWeightPercent, decimal MaxFinalWeightPercent,
    decimal PassingGrade, decimal RawScoreFloor1, decimal RawScoreFloor2, decimal FinalExamFloor,
    int AAMin, int BAMin, int BBMin, int CBMin, int CCMin, int DCMin, int DDMin,
    bool LetterRangesEditable,
    string BelowFloorGrade,
    string EvaluationType,
    int RelativeMinStudentCount,
    decimal RelativeInclusionLimit,
    decimal RelativeAbsoluteFloor,
    int TAAMin, int TBAMin, int TBBMin, int TCBMin, int TCCMin, int TDCMin, int TDDMin,
    bool BlockFinalIfNoMidterm,
    bool FailIfNoFinal,
    bool BlockMakeupIfNoFinal,
    bool ZeroIfMissedMakeup,
    bool MakeupReplacesFinal,
    bool AutoTransferFailedToMakeup,
    bool AutoTransferExcusedToMakeup);

public record SaveExamRegulationRequest(
    int ProgramId, DegreeLevel DegreeLevel, TermType TermType, int Year,
    bool IsActive, string Name, int MidtermCount, int FinalCount,
    decimal MinMidtermWeightPercent, decimal MaxMidtermWeightPercent,
    decimal MinFinalWeightPercent, decimal MaxFinalWeightPercent,
    decimal? PassingGrade, decimal RawScoreFloor1, decimal RawScoreFloor2, decimal FinalExamFloor,
    int AAMin = 91, int BAMin = 81, int BBMin = 71, int CBMin = 61,
    int CCMin = 55, int DCMin = 50, int DDMin = 45, bool LetterRangesEditable = true,
    GradeCode BelowFloorGrade = GradeCode.FF,
    GradeEvaluationType EvaluationType = GradeEvaluationType.Absolute,
    int RelativeMinStudentCount = 10,
    decimal RelativeInclusionLimit = 0,
    decimal RelativeAbsoluteFloor = 0,
    int TAAMin = 70, int TBAMin = 63, int TBBMin = 57, int TCBMin = 50,
    int TCCMin = 45, int TDCMin = 40, int TDDMin = 35,
    bool BlockFinalIfNoMidterm = false,
    bool FailIfNoFinal = false,
    bool BlockMakeupIfNoFinal = false,
    bool ZeroIfMissedMakeup = false,
    bool MakeupReplacesFinal = false,
    bool AutoTransferFailedToMakeup = false,
    bool AutoTransferExcusedToMakeup = false);

public record CloneExamRegulationRequest(int? ProgramId, int Year);

public record AttendancePolicyDto(
    int TermWeekCount,
    decimal TheoryAbsenceLimitPercent,
    decimal LabAbsenceLimitPercent,
    string AttendanceMethod = "Separate");
