using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.API.Security;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/schedule-excel")]
[Authorize]
public class ScheduleExcelController : ControllerBase
{
    private readonly IExcelScheduleService _excelService;
    private readonly IAcademicScopeGuard _scopeGuard;

    public ScheduleExcelController(IExcelScheduleService excelService, IAcademicScopeGuard scopeGuard)
    {
        _excelService = excelService;
        _scopeGuard = scopeGuard;
    }

    [HttpGet("export")]
    [RequirePermission("menu.ders-programi-olusturma.export")]
    public async Task<IActionResult> ExportSchedule(
        [FromQuery] int termId,
        [FromQuery] int facultyId,
        [FromQuery] int? departmentId = null,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (!await CanAccessScheduleScopeAsync(userId, facultyId, departmentId, ct))
            return Forbid();

        var bytes = await _excelService.ExportScheduleAsync(termId, facultyId, departmentId, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ders-programi.xlsx");
    }

    [HttpGet("export-pdf")]
    [RequirePermission("menu.ders-programi-olusturma.export-pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] int termId,
        [FromQuery] int facultyId,
        [FromQuery] int? departmentId = null,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (!await CanAccessScheduleScopeAsync(userId, facultyId, departmentId, ct))
            return Forbid();

        var bytes = await _excelService.ExportPdfAsync(termId, facultyId, departmentId, ct);
        var fileName = departmentId.HasValue ? "ders-programi.pdf" : "ders-programi-tum.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpPost("import-preview")]
    [RequirePermission("menu.ders-programi-olusturma.import")]
    [RequestSizeLimit(FileUploadValidation.MaxExcelImportBytes)]
    public async Task<ActionResult<ImportPreviewDto>> ImportPreview(
        [FromQuery] int termId,
        [FromQuery] int facultyId,
        IFormFile file,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (!await _scopeGuard.CanAccessFacultyAsync(User, userId, facultyId, ct))
            return Forbid();

        var fileValidationError = FileUploadValidation.ValidateXlsx(file);
        if (fileValidationError is not null)
            return BadRequest(new { error = fileValidationError });

        using var stream = file.OpenReadStream();
        try
        {
            var result = await _excelService.PreviewImportAsync(userId, stream, termId, facultyId, ct);
            return Ok(result);
        }
        catch (ScheduleImportSessionInvalidException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("import-apply")]
    [RequirePermission("menu.ders-programi-olusturma.import")]
    public async Task<ActionResult<ImportResultDto>> ImportApply(
        [FromBody] ImportApplyRequest request,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (!await _scopeGuard.CanAccessFacultyAsync(User, userId, request.FacultyId, ct))
            return Forbid();

        try
        {
            var result = await _excelService.ApplyImportAsync(
                userId,
                request.FacultyId,
                request.TermId,
                request.ImportSessionId,
                request.RowNumbers,
                ct);
            return Ok(result);
        }
        catch (ScheduleImportSessionForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (ScheduleImportSessionInvalidException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool TryGetUserId(out int userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out userId);
    }

    private async Task<bool> CanAccessScheduleScopeAsync(
        int userId,
        int facultyId,
        int? departmentId,
        CancellationToken ct)
    {
        if (!await _scopeGuard.CanAccessFacultyAsync(User, userId, facultyId, ct))
            return false;

        return !departmentId.HasValue
            || await _scopeGuard.CanAccessDepartmentAsync(User, userId, departmentId.Value, ct);
    }
}

public class ImportApplyRequest
{
    public int FacultyId { get; set; }
    public int TermId { get; set; }
    public Guid ImportSessionId { get; set; }
    public List<int> RowNumbers { get; set; } = new();
}
