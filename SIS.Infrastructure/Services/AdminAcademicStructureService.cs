using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Admin.Services;
using SIS.Application.Identity.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Scope;

namespace SIS.Infrastructure.Services;

public class AdminAcademicStructureService(
    AppDbContext db,
    IScopeGuard scopeGuard,
    IScopeResolver scopeResolver) : IAdminAcademicStructureService
{
    // Alan uzunluk limitleri (tek otorite — DB kolonları text olduğu için
    // veri tutarlılığı bu katmanda korunur; tüm yazımlar buradan geçer).
    private const int MaxNameLength = 150;
    private const int MaxCodeLength = 20;

    private static string NormalizeRequiredText(string value, string fieldName, int maxLength = MaxNameLength)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException($"{fieldName} boş bırakılamaz.");
        if (normalized.Length > maxLength)
            throw new InvalidOperationException($"{fieldName} en fazla {maxLength} karakter olabilir.");

        return normalized;
    }

    private static string NormalizeCode(string value, string fieldName)
    {
        var normalized = NormalizeRequiredText(value, fieldName, MaxCodeLength).ToUpperInvariant();
        if (normalized.Contains(' '))
            throw new InvalidOperationException($"{fieldName} boşluk içeremez.");

        return normalized;
    }

    private static int RequireMaxStudyDuration(int? value) =>
        value ?? throw new InvalidOperationException("Azami öğrenim süresi gönderilmelidir.");

    private static string? NormalizeOptionalCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToUpperInvariant();
        const int maxExternalCodeLength = 30; // AppDbContext: ExternalSystemCode HasMaxLength(30)
        if (normalized.Length > maxExternalCodeLength)
            throw new InvalidOperationException($"Dış sistem kodu en fazla {maxExternalCodeLength} karakter olabilir.");

        return normalized;
    }

    public async Task<List<FacultyDto>> GetFacultiesAsync()
    {
        var rows = await db.Faculties
            .OrderBy(f => f.Name)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Code,
                f.CreatedAt,
                f.UpdatedAt,
                DepartmentCount = f.Departments.Count,
                ProgramCount = f.Departments.SelectMany(d => d.Programs).Count()
            })
            .ToListAsync();

        var studentByFaculty = await db.StudentPrograms
            .GroupBy(sp => sp.Program.Department.FacultyId)
            .Select(g => new { FacultyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FacultyId, x => x.Count);

        return rows
            .Select(f => new FacultyDto(
                f.Id,
                f.Name,
                f.Code,
                f.DepartmentCount,
                f.ProgramCount,
                studentByFaculty.GetValueOrDefault(f.Id),
                f.CreatedAt,
                f.UpdatedAt))
            .ToList();
    }

    public async Task<FacultyDto> CreateFacultyAsync(CreateFacultyDto dto)
    {
        var name = NormalizeRequiredText(dto.Name, "Fakülte adı");
        var code = NormalizeCode(dto.Code, "Fakülte kodu");

        if (await db.Faculties.AnyAsync(f => f.Code == code))
            throw new InvalidOperationException($"'{code}' kodlu fakülte zaten mevcut.");

        var faculty = new Faculty { Name = name, Code = code, CreatedAt = DateTime.UtcNow };
        db.Faculties.Add(faculty);
        await db.SaveChangesAsync();
        return new FacultyDto(faculty.Id, faculty.Name, faculty.Code, 0, 0, 0, faculty.CreatedAt, faculty.UpdatedAt);
    }

    public async Task<BulkImportResultDto> BulkCreateFacultiesAsync(IReadOnlyList<CreateFacultyDto> items)
    {
        var rows = new List<BulkRowResultDto>(items.Count);
        var created = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var label = string.IsNullOrWhiteSpace(items[i].Code) ? items[i].Name : items[i].Code;
            try
            {
                await CreateFacultyAsync(items[i]);
                created++;
                rows.Add(new BulkRowResultDto(i, label, "created", null));
            }
            catch (InvalidOperationException ex)
            {
                rows.Add(new BulkRowResultDto(i, label, "error", ex.Message));
            }
        }
        return new BulkImportResultDto(created, items.Count - created, rows);
    }

    public async Task<FacultyDto> UpdateFacultyAsync(int id, UpdateFacultyDto dto)
    {
        var name = NormalizeRequiredText(dto.Name, "Fakülte adı");
        var code = NormalizeCode(dto.Code, "Fakülte kodu");

        var faculty = await db.Faculties.FindAsync(id)
            ?? throw new KeyNotFoundException("Fakülte bulunamadı.");
        if (await db.Faculties.AnyAsync(f => f.Code == code && f.Id != id))
            throw new InvalidOperationException($"'{code}' kodu başka bir fakülte tarafından kullanılıyor.");

        faculty.Name = name;
        faculty.Code = code;
        faculty.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var deptCount = await db.Departments.CountAsync(d => d.FacultyId == id);
        var programCount = await db.AcademicPrograms.CountAsync(p => p.Department.FacultyId == id);
        var studentCount = await db.StudentPrograms.CountAsync(sp => sp.Program.Department.FacultyId == id);
        return new FacultyDto(faculty.Id, faculty.Name, faculty.Code, deptCount, programCount, studentCount, faculty.CreatedAt, faculty.UpdatedAt);
    }

    public async Task<FacultyDeletionSummaryDto> GetFacultyDeletionSummaryAsync(int id)
    {
        var faculty = await db.Faculties.FindAsync(id)
            ?? throw new KeyNotFoundException("Fakülte bulunamadı.");

        var departmentCount = await db.Departments.CountAsync(d => d.FacultyId == id);
        var programCount = await db.AcademicPrograms.CountAsync(p => p.Department.FacultyId == id);
        var curriculumCount = await db.Curricula.CountAsync(c => c.Program.Department.FacultyId == id);
        var studentCount = await db.StudentPrograms
            .Where(sp => sp.Program.Department.FacultyId == id)
            .Select(sp => sp.StudentId)
            .Distinct()
            .CountAsync();
        var instructorCount = await db.Instructors.CountAsync(i => i.Department.FacultyId == id);

        return new FacultyDeletionSummaryDto(
            faculty.Id,
            faculty.Name,
            departmentCount,
            programCount,
            curriculumCount,
            studentCount,
            instructorCount,
            CanDelete: departmentCount == 0);
    }

    public async Task<FacultyOverviewDto> GetFacultyOverviewAsync(int id)
    {
        var faculty = await db.Faculties.FindAsync(id)
            ?? throw new KeyNotFoundException("Fakülte bulunamadı.");

        var deptRows = await db.Departments
            .Where(d => d.FacultyId == id)
            .OrderBy(d => d.Name)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Code,
                d.IsServiceUnit,
                ProgramCount = d.Programs.Count
            })
            .ToListAsync();

        var studentByDept = await db.StudentPrograms
            .Where(sp => sp.Program.Department.FacultyId == id)
            .GroupBy(sp => sp.Program.DepartmentId)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count);

        var instructorByDept = await db.Instructors
            .Where(i => i.Department.FacultyId == id)
            .GroupBy(i => i.DepartmentId)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count);

        var departments = deptRows
            .Select(d => new FacultyDepartmentStatDto(
                d.Id,
                d.Name,
                d.Code,
                d.IsServiceUnit,
                studentByDept.GetValueOrDefault(d.Id),
                instructorByDept.GetValueOrDefault(d.Id),
                d.ProgramCount))
            .ToList();

        var programsByDegree = await db.AcademicPrograms
            .Where(p => p.Department.FacultyId == id)
            .GroupBy(p => p.DegreeLevel)
            .Select(g => new { Degree = g.Key, Count = g.Count() })
            .ToListAsync();

        var curriculumCount = await db.Curricula.CountAsync(c => c.Program.Department.FacultyId == id);
        var studentTotal = await db.StudentPrograms.CountAsync(sp => sp.Program.Department.FacultyId == id);
        var programsWithoutCurriculum = await db.AcademicPrograms
            .CountAsync(p => p.Department.FacultyId == id && p.Curricula.Count == 0);
        var programsWithoutExternalCode = await db.AcademicPrograms
            .CountAsync(p => p.Department.FacultyId == id && p.ExternalSystemCode == null);

        return new FacultyOverviewDto(
            faculty.Id,
            faculty.Name,
            faculty.Code,
            faculty.CreatedAt,
            faculty.UpdatedAt,
            departments.Count,
            departments.Sum(d => d.ProgramCount),
            curriculumCount,
            studentTotal,
            departments.Sum(d => d.InstructorCount),
            programsWithoutCurriculum,
            programsWithoutExternalCode,
            departments,
            programsByDegree
                .Select(x => new DegreeLevelCountDto(x.Degree.ToString(), x.Count))
                .ToList());
    }

    public async Task DeleteFacultyAsync(int id)
    {
        if (await db.Departments.AnyAsync(d => d.FacultyId == id))
            throw new InvalidOperationException("Bu fakülteye bağlı bölümler var. Önce bölümleri silin.");

        var faculty = await db.Faculties.FindAsync(id)
            ?? throw new KeyNotFoundException("Fakülte bulunamadı.");
        db.Faculties.Remove(faculty);
        await db.SaveChangesAsync();
    }

    public async Task<List<DepartmentDto>> GetDepartmentsAsync(int? facultyId = null)
    {
        var query = db.Departments.Include(d => d.Faculty).AsQueryable();
        if (facultyId.HasValue)
            query = query.Where(d => d.FacultyId == facultyId.Value);

        var instructorCounts = await db.Instructors
            .GroupBy(i => i.DepartmentId)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count);

        var studentCounts = await db.StudentPrograms
            .GroupBy(sp => sp.Program.DepartmentId)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count);

        var rows = await query
            .OrderBy(d => d.Faculty.Name)
            .ThenBy(d => d.Name)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Code,
                d.FacultyId,
                FacultyName = d.Faculty.Name,
                ProgramCount = d.Programs.Count,
                d.IsServiceUnit,
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync();

        return rows.Select(d => new DepartmentDto(
            d.Id,
            d.Name,
            d.Code,
            d.FacultyId,
            d.FacultyName,
            d.ProgramCount,
            studentCounts.TryGetValue(d.Id, out var studentCount) ? studentCount : 0,
            instructorCounts.TryGetValue(d.Id, out var instructorCount) ? instructorCount : 0,
            d.IsServiceUnit,
            d.CreatedAt,
            d.UpdatedAt)).ToList();
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto dto)
    {
        scopeGuard.EnsureCanWrite(ScopeKind.Faculty, dto.FacultyId);

        var name = NormalizeRequiredText(dto.Name, "Bölüm adı");
        var code = NormalizeCode(dto.Code, "Bölüm kodu");

        if (!await db.Faculties.AnyAsync(f => f.Id == dto.FacultyId))
            throw new KeyNotFoundException("Fakülte bulunamadı.");
        if (await db.Departments.AnyAsync(d => d.Code == code))
            throw new InvalidOperationException($"'{code}' kodlu bölüm zaten mevcut.");

        var department = new Department
        {
            Name = name,
            Code = code,
            FacultyId = dto.FacultyId,
            CreatedAt = DateTime.UtcNow
        };

        db.Departments.Add(department);
        await db.SaveChangesAsync();

        var facultyName = (await db.Faculties.FindAsync(dto.FacultyId))!.Name;
        return new DepartmentDto(
            department.Id,
            department.Name,
            department.Code,
            department.FacultyId,
            facultyName,
            ProgramCount: 0,
            StudentCount: 0,
            InstructorCount: 0,
            IsServiceUnit: department.IsServiceUnit,
            CreatedAt: department.CreatedAt,
            UpdatedAt: department.UpdatedAt);
    }

    public async Task<BulkImportResultDto> BulkCreateDepartmentsAsync(IReadOnlyList<CreateDepartmentDto> items)
    {
        var rows = new List<BulkRowResultDto>(items.Count);
        var created = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var label = string.IsNullOrWhiteSpace(items[i].Code) ? items[i].Name : items[i].Code;
            try
            {
                await CreateDepartmentAsync(items[i]);
                created++;
                rows.Add(new BulkRowResultDto(i, label, "created", null));
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
            {
                rows.Add(new BulkRowResultDto(i, label, "error", ex.Message));
            }
        }
        return new BulkImportResultDto(created, items.Count - created, rows);
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(int id, UpdateDepartmentDto dto)
    {
        var name = NormalizeRequiredText(dto.Name, "Bölüm adı");
        var code = NormalizeCode(dto.Code, "Bölüm kodu");

        var department = await db.Departments.Include(d => d.Faculty).FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException("Bölüm bulunamadı.");
        if (department.FacultyId != dto.FacultyId)
            scopeGuard.EnsureCanWrite(ScopeKind.Faculty, dto.FacultyId);
        if (!await db.Faculties.AnyAsync(f => f.Id == dto.FacultyId))
            throw new KeyNotFoundException("Fakülte bulunamadı.");
        if (await db.Departments.AnyAsync(d => d.Code == code && d.Id != id))
            throw new InvalidOperationException($"'{code}' kodu başka bir bölüm tarafından kullanılıyor.");

        department.Name = name;
        department.Code = code;
        department.FacultyId = dto.FacultyId;
        department.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var programCount = await db.AcademicPrograms.CountAsync(p => p.DepartmentId == id);
        var instructorCount = await db.Instructors.CountAsync(i => i.DepartmentId == id);
        var studentCount = await db.StudentPrograms.CountAsync(sp => sp.Program.DepartmentId == id);
        return new DepartmentDto(
            department.Id,
            department.Name,
            department.Code,
            department.FacultyId,
            department.Faculty.Name,
            programCount,
            studentCount,
            instructorCount,
            department.IsServiceUnit,
            department.CreatedAt,
            department.UpdatedAt);
    }

    public async Task<DepartmentOverviewDto> GetDepartmentOverviewAsync(int id)
    {
        var department = await db.Departments
            .Include(d => d.Faculty)
            .FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException("Bölüm bulunamadı.");

        var programRows = await db.AcademicPrograms
            .Where(p => p.DepartmentId == id)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Code,
                p.DegreeLevel,
                p.EducationType,
                p.TotalRequiredEcts,
                p.MaxStudyDurationSemesters,
                CurriculumCount = p.Curricula.Count,
                HasExternalCode = p.ExternalSystemCode != null
            })
            .ToListAsync();

        var studentByProgram = await db.StudentPrograms
            .Where(sp => sp.Program.DepartmentId == id)
            .GroupBy(sp => sp.ProgramId)
            .Select(g => new { ProgramId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProgramId, x => x.Count);

        var programs = programRows
            .Select(p => new DepartmentProgramStatDto(
                p.Id,
                p.Name,
                p.Code,
                p.DegreeLevel.ToString(),
                p.EducationType.ToString(),
                p.TotalRequiredEcts,
                p.MaxStudyDurationSemesters,
                p.CurriculumCount,
                p.HasExternalCode,
                studentByProgram.GetValueOrDefault(p.Id)))
            .ToList();

        var studentTotal = await db.StudentPrograms.CountAsync(sp => sp.Program.DepartmentId == id);

        var instructorRows = await db.Instructors
            .Where(i => i.DepartmentId == id)
            .Select(i => new { i.Id, i.UserId, i.Title })
            .ToListAsync();
        var instructorUserIds = instructorRows.Select(i => i.UserId).ToList();
        var instructorNames = await db.Users
            .Where(u => instructorUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
        var instructors = instructorRows
            .Select(i => new DepartmentInstructorDto(
                i.Id,
                instructorNames.GetValueOrDefault(i.UserId, "—"),
                i.Title))
            .OrderBy(i => i.Name)
            .ToList();

        var programsByDegree = programRows
            .GroupBy(p => p.DegreeLevel)
            .Select(g => new DegreeLevelCountDto(g.Key.ToString(), g.Count()))
            .ToList();

        return new DepartmentOverviewDto(
            department.Id,
            department.Name,
            department.Code,
            department.FacultyId,
            department.Faculty.Name,
            department.IsServiceUnit,
            department.CreatedAt,
            department.UpdatedAt,
            studentTotal,
            instructors.Count,
            programs.Count,
            programs.Sum(p => p.CurriculumCount),
            programs.Count(p => p.CurriculumCount == 0),
            programs.Count(p => !p.HasExternalCode),
            programs,
            instructors,
            programsByDegree);
    }

    public async Task<DepartmentDeletionSummaryDto> GetDepartmentDeletionSummaryAsync(int id)
    {
        var department = await db.Departments.FindAsync(id)
            ?? throw new KeyNotFoundException("Bölüm bulunamadı.");

        var programCount = await db.AcademicPrograms.CountAsync(p => p.DepartmentId == id);
        var curriculumCount = await db.Curricula.CountAsync(c => c.Program.DepartmentId == id);
        var studentCount = await db.StudentPrograms
            .Where(sp => sp.Program.DepartmentId == id)
            .Select(sp => sp.StudentId)
            .Distinct()
            .CountAsync();
        var instructorCount = await db.Instructors.CountAsync(i => i.DepartmentId == id);

        return new DepartmentDeletionSummaryDto(
            department.Id,
            department.Name,
            programCount,
            curriculumCount,
            studentCount,
            instructorCount,
            CanDelete: programCount == 0 && instructorCount == 0);
    }

    public async Task DeleteDepartmentAsync(int id)
    {
        if (await db.AcademicPrograms.AnyAsync(p => p.DepartmentId == id))
            throw new InvalidOperationException("Bu bölüme bağlı programlar var. Önce programları silin.");
        if (await db.Instructors.AnyAsync(i => i.DepartmentId == id))
            throw new InvalidOperationException("Bu bölüme atanmış öğretim üyeleri var.");

        var department = await db.Departments.FindAsync(id)
            ?? throw new KeyNotFoundException("Bölüm bulunamadı.");
        db.Departments.Remove(department);
        await db.SaveChangesAsync();
    }

    public async Task<List<ProgramDto>> GetProgramsAsync(int? departmentId = null)
    {
        var query = db.AcademicPrograms.Include(p => p.Department).AsQueryable();
        if (departmentId.HasValue)
            query = query.Where(p => p.DepartmentId == departmentId.Value);

        return await query
            .OrderBy(p => p.Department.Name)
            .ThenBy(p => p.Name)
            .Select(p => new ProgramDto(
                p.Id,
                p.Name,
                p.Code,
                p.ExternalSystemCode,
                p.DepartmentId,
                p.Department.Name,
                p.DegreeLevel.ToString(),
                p.EducationType.ToString(),
                p.MaxStudyDurationSemesters,
                p.TotalRequiredEcts,
                p.Curricula.Count,
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync();
    }

    public async Task<ProgramDto> CreateProgramAsync(CreateProgramDto dto)
    {
        var facultyId = await scopeResolver.ResolveFacultyFromDepartmentAsync(dto.DepartmentId);
        scopeGuard.EnsureCanWrite(ScopeKind.Faculty, facultyId);

        var programName = NormalizeRequiredText(dto.Name, "Program adı");
        var programCode = NormalizeCode(dto.Code, "Program kodu");
        var externalSystemCode = NormalizeOptionalCode(dto.ExternalSystemCode);
        var maxStudyDuration = RequireMaxStudyDuration(dto.MaxStudyDurationSemesters);

        if (!await db.Departments.AnyAsync(d => d.Id == dto.DepartmentId))
            throw new KeyNotFoundException("Bölüm bulunamadı.");
        if (await db.AcademicPrograms.AnyAsync(p => p.Code == programCode))
            throw new InvalidOperationException($"'{programCode}' kodlu program zaten mevcut.");
        if (externalSystemCode is not null &&
            await db.AcademicPrograms.AnyAsync(p => p.ExternalSystemCode == externalSystemCode))
            throw new InvalidOperationException($"'{externalSystemCode}' dış sistem kodu başka bir program tarafından kullanılıyor.");

        var program = new AcademicProgram
        {
            Name = programName,
            Code = programCode,
            ExternalSystemCode = externalSystemCode,
            DepartmentId = dto.DepartmentId,
            DegreeLevel = Enum.Parse<DegreeLevel>(dto.DegreeLevel),
            EducationType = Enum.Parse<EducationType>(dto.EducationType),
            MaxStudyDurationSemesters = maxStudyDuration,
            TotalRequiredEcts = dto.TotalRequiredEcts,
            CreatedAt = DateTime.UtcNow
        };

        db.AcademicPrograms.Add(program);
        await db.SaveChangesAsync();

        var departmentName = (await db.Departments.FindAsync(dto.DepartmentId))!.Name;
        return new ProgramDto(
            program.Id,
            program.Name,
            program.Code,
            program.ExternalSystemCode,
            program.DepartmentId,
            departmentName,
            program.DegreeLevel.ToString(),
            program.EducationType.ToString(),
            program.MaxStudyDurationSemesters,
            program.TotalRequiredEcts,
            0,
            program.CreatedAt,
            program.UpdatedAt);
    }

    public async Task<ProgramDto> UpdateProgramAsync(int id, UpdateProgramDto dto)
    {
        var programName = NormalizeRequiredText(dto.Name, "Program adı");
        var programCode = NormalizeCode(dto.Code, "Program kodu");
        var externalSystemCode = NormalizeOptionalCode(dto.ExternalSystemCode);
        var maxStudyDuration = RequireMaxStudyDuration(dto.MaxStudyDurationSemesters);

        var program = await db.AcademicPrograms.Include(p => p.Department).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException("Program bulunamadı.");
        if (program.DepartmentId != dto.DepartmentId)
        {
            var facultyId = await scopeResolver.ResolveFacultyFromDepartmentAsync(dto.DepartmentId);
            scopeGuard.EnsureCanWrite(ScopeKind.Faculty, facultyId);
        }
        if (!await db.Departments.AnyAsync(d => d.Id == dto.DepartmentId))
            throw new KeyNotFoundException("Bölüm bulunamadı.");
        if (await db.AcademicPrograms.AnyAsync(p => p.Code == programCode && p.Id != id))
            throw new InvalidOperationException($"'{programCode}' kodu başka bir program tarafından kullanılıyor.");
        if (externalSystemCode is not null &&
            await db.AcademicPrograms.AnyAsync(p => p.ExternalSystemCode == externalSystemCode && p.Id != id))
            throw new InvalidOperationException($"'{externalSystemCode}' dış sistem kodu başka bir program tarafından kullanılıyor.");

        program.Name = programName;
        program.Code = programCode;
        program.ExternalSystemCode = externalSystemCode;
        program.DepartmentId = dto.DepartmentId;
        program.DegreeLevel = Enum.Parse<DegreeLevel>(dto.DegreeLevel);
        program.EducationType = Enum.Parse<EducationType>(dto.EducationType);
        program.MaxStudyDurationSemesters = maxStudyDuration;
        program.TotalRequiredEcts = dto.TotalRequiredEcts;
        program.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var curriculumCount = await db.Curricula.CountAsync(c => c.ProgramId == id);
        return new ProgramDto(
            program.Id,
            program.Name,
            program.Code,
            program.ExternalSystemCode,
            program.DepartmentId,
            program.Department.Name,
            program.DegreeLevel.ToString(),
            program.EducationType.ToString(),
            program.MaxStudyDurationSemesters,
            program.TotalRequiredEcts,
            curriculumCount,
            program.CreatedAt,
            program.UpdatedAt);
    }

    public async Task<ProgramDeletionSummaryDto> GetProgramDeletionSummaryAsync(int id)
    {
        var program = await db.AcademicPrograms.FindAsync(id)
            ?? throw new KeyNotFoundException("Program bulunamadı.");

        var curriculumCount = await db.Curricula.CountAsync(c => c.ProgramId == id);
        var studentCount = await db.StudentPrograms
            .Where(sp => sp.ProgramId == id)
            .Select(sp => sp.StudentId)
            .Distinct()
            .CountAsync();

        return new ProgramDeletionSummaryDto(
            program.Id,
            program.Name,
            curriculumCount,
            studentCount,
            CanDelete: studentCount == 0);
    }

    public async Task DeleteProgramAsync(int id)
    {
        if (await db.StudentPrograms.AnyAsync(sp => sp.ProgramId == id))
            throw new InvalidOperationException("Bu programa kayıtlı öğrenciler var. Program silinemez.");

        var program = await db.AcademicPrograms.FindAsync(id)
            ?? throw new KeyNotFoundException("Program bulunamadı.");
        db.AcademicPrograms.Remove(program);
        await db.SaveChangesAsync();
    }
}
