namespace SIS.Application.Admin.DTOs;

public record FacultyDto(int Id, string Name, string Code, int DepartmentCount, int ProgramCount, int StudentCount, DateTime CreatedAt, DateTime? UpdatedAt);

// ─── Faculty Overview (zengin detay paneli) ───────────────────────────────────

/// <summary>Fakülte detay panelinde bölüm bazlı kırılım satırı.</summary>
public record FacultyDepartmentStatDto(
    int Id,
    string Name,
    string Code,
    bool IsServiceUnit,
    int StudentCount,
    int InstructorCount,
    int ProgramCount);

/// <summary>Derece düzeyine göre program sayısı (donut için).</summary>
public record DegreeLevelCountDto(string DegreeLevel, int Count);

/// <summary>Bölüm detayında öğretim üyesi satırı (isim + unvan).</summary>
public record DepartmentInstructorDto(int Id, string Name, string Title);

/// <summary>Bölüm detay panelinde program bazlı kırılım satırı.</summary>
public record DepartmentProgramStatDto(
    int Id,
    string Name,
    string Code,
    string DegreeLevel,
    string EducationType,
    int TotalRequiredEcts,
    int MaxStudyDurationSemesters,
    int CurriculumCount,
    bool HasExternalCode,
    int StudentCount);

/// <summary>
/// Bölüm detay panelinin tüm verisini tek çağrıda döndürür:
/// toplamlar + program kırılımı + derece dağılımı + hazırlık metrikleri.
/// </summary>
public record DepartmentOverviewDto(
    int Id,
    string Name,
    string Code,
    int FacultyId,
    string FacultyName,
    bool IsServiceUnit,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int StudentCount,
    int InstructorCount,
    int ProgramCount,
    int CurriculumCount,
    int ProgramsWithoutCurriculum,
    int ProgramsWithoutExternalCode,
    IReadOnlyList<DepartmentProgramStatDto> Programs,
    IReadOnlyList<DepartmentInstructorDto> Instructors,
    IReadOnlyList<DegreeLevelCountDto> ProgramsByDegree);

/// <summary>
/// Fakülte detay panelinin tüm verisini tek çağrıda döndürür:
/// toplamlar + bölüm kırılımı + program derece dağılımı.
/// </summary>
public record FacultyOverviewDto(
    int Id,
    string Name,
    string Code,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int DepartmentCount,
    int ProgramCount,
    int CurriculumCount,
    int StudentCount,
    int InstructorCount,
    int ProgramsWithoutCurriculum,
    int ProgramsWithoutExternalCode,
    IReadOnlyList<FacultyDepartmentStatDto> Departments,
    IReadOnlyList<DegreeLevelCountDto> ProgramsByDegree);

/// <summary>
/// Fakülte silmeden önce gösterilecek gerçek etki özeti. CanDelete, DeleteFacultyAsync
/// guard'ıyla aynı kuralı yansıtır: bağlı bölüm yoksa (DepartmentCount == 0) silinebilir.
/// </summary>
public record FacultyDeletionSummaryDto(
    int FacultyId,
    string FacultyName,
    int DepartmentCount,
    int ProgramCount,
    int CurriculumCount,
    int StudentCount,
    int InstructorCount,
    bool CanDelete);

/// <summary>
/// Bölüm silmeden önceki etki özeti. CanDelete, DeleteDepartmentAsync guard'ıyla aynı:
/// bağlı program ve öğretim üyesi yoksa silinebilir.
/// </summary>
public record DepartmentDeletionSummaryDto(
    int DepartmentId,
    string DepartmentName,
    int ProgramCount,
    int CurriculumCount,
    int StudentCount,
    int InstructorCount,
    bool CanDelete);

/// <summary>
/// Program silmeden önceki etki özeti. CanDelete, DeleteProgramAsync guard'ıyla aynı:
/// kayıtlı öğrenci yoksa silinebilir.
/// </summary>
public record ProgramDeletionSummaryDto(
    int ProgramId,
    string ProgramName,
    int CurriculumCount,
    int StudentCount,
    bool CanDelete);

public record DepartmentDto(
    int Id,
    string Name,
    string Code,
    int FacultyId,
    string FacultyName,
    int ProgramCount,
    int StudentCount,
    int InstructorCount,
    bool IsServiceUnit,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ProgramDto(
    int Id, string Name, string Code,
    string? ExternalSystemCode,
    int DepartmentId, string DepartmentName,
    string DegreeLevel, string EducationType,
    int MaxStudyDurationSemesters, int TotalRequiredEcts,
    int CurriculumCount,
    DateTime CreatedAt, DateTime? UpdatedAt
);

/// <summary>Toplu içe aktarmada tek bir satırın sonucu.</summary>
public record BulkRowResultDto(int Index, string Label, string Status, string? Message);

/// <summary>Toplu içe aktarma özeti (satır bazlı sonuçlarla).</summary>
public record BulkImportResultDto(int CreatedCount, int FailedCount, IReadOnlyList<BulkRowResultDto> Rows);

public record CreateFacultyDto(string Name, string Code);
public record UpdateFacultyDto(string Name, string Code);

public record CreateDepartmentDto(string Name, string Code, int FacultyId);
public record UpdateDepartmentDto(string Name, string Code, int FacultyId);

public record CreateProgramDto(
    string Name, string Code,
    string? ExternalSystemCode,
    int DepartmentId,
    string DegreeLevel,
    string EducationType,
    int? MaxStudyDurationSemesters,
    int TotalRequiredEcts
);
public record UpdateProgramDto(
    string Name, string Code,
    string? ExternalSystemCode,
    int DepartmentId,
    string DegreeLevel,
    string EducationType,
    int? MaxStudyDurationSemesters,
    int TotalRequiredEcts
);
