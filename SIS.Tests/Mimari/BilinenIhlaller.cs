namespace SIS.Tests.Mimari;

/// <summary>
/// Mimari kuralların bugünkü bilinen ihlalleri (bilinen borç; örnek alınmaz).
/// Kural başına sıralı (ordinal) ve tekrarsız liste; kayıtlar test çıktısından alınmıştır.
/// Liste yalnız küçülür: yeni kayıt eklenmez; düzelen ihlal listeden düşülür, düşülmezse test kırılır.
/// </summary>
internal static class BilinenIhlaller
{
    /// <summary>K1: SIS.Domain'in System.* dışındaki derleme başvuruları.</summary>
    public static readonly string[] K1DomainBasvurulari = [];

    /// <summary>K2: SIS.Application'ın System.* ve izin listesi dışındaki derleme başvuruları.</summary>
    public static readonly string[] K2ApplicationBasvurulari = [];

    /// <summary>K3: imzasında DbContext türevi olan denetleyiciler (tür tam adı).</summary>
    public static readonly string[] K3DbContexteBaglananDenetleyiciler =
    [
        "SIS.API.Controllers.AcademicLevelController",
        "SIS.API.Controllers.AcademicProgressionController",
        "SIS.API.Controllers.Admin.AdminBenchmarkController",
        "SIS.API.Controllers.Admin.AdminSeedingController",
        "SIS.API.Controllers.Admin.MenuAdminController",
        "SIS.API.Controllers.Admin.PermissionAdminController",
        "SIS.API.Controllers.AdminLookupController",
        "SIS.API.Controllers.AdvisorConstraintController",
        "SIS.API.Controllers.AdvisorController",
        "SIS.API.Controllers.AttendanceController",
        "SIS.API.Controllers.AttendancePolicyController",
        "SIS.API.Controllers.AuditLogController",
        "SIS.API.Controllers.AuthController",
        "SIS.API.Controllers.ClassroomController",
        "SIS.API.Controllers.ClassroomExportController",
        "SIS.API.Controllers.ClassroomTermAssignmentController",
        "SIS.API.Controllers.ConstraintController",
        "SIS.API.Controllers.CourseAccessPolicyController",
        "SIS.API.Controllers.CourseAccessRuleController",
        "SIS.API.Controllers.CourseCatalogAdminController",
        "SIS.API.Controllers.CourseController",
        "SIS.API.Controllers.CourseLookupsController",
        "SIS.API.Controllers.CourseOpeningController",
        "SIS.API.Controllers.CurriculumController",
        "SIS.API.Controllers.DepartmentScheduleController",
        "SIS.API.Controllers.DistributionConstraintController",
        "SIS.API.Controllers.ElectiveGroupController",
        "SIS.API.Controllers.ElectiveSubtypeController",
        "SIS.API.Controllers.EligibilityController",
        "SIS.API.Controllers.ExamRegulationController",
        "SIS.API.Controllers.ExceptionRequestController",
        "SIS.API.Controllers.FacultyPlanningController",
        "SIS.API.Controllers.ForecastController",
        "SIS.API.Controllers.GradeController",
        "SIS.API.Controllers.InstructorController",
        "SIS.API.Controllers.InstructorManagementController",
        "SIS.API.Controllers.InternalAnnouncementController",
        "SIS.API.Controllers.PromotionRuleController",
        "SIS.API.Controllers.RegistrationController",
        "SIS.API.Controllers.ScheduleApprovalController",
        "SIS.API.Controllers.SchedulingAlgorithmConfigController",
        "SIS.API.Controllers.SchedulingController",
        "SIS.API.Controllers.SectionExamController",
        "SIS.API.Controllers.StudentCardController",
        "SIS.API.Controllers.StudentController",
        "SIS.API.Controllers.StudentIntegrationController",
        "SIS.API.Controllers.StudentManagementController",
        "SIS.API.Controllers.StudentPlacementController",
        "SIS.API.Controllers.StudentScholarshipController",
        "SIS.API.Controllers.StudentTimetableController",
        "SIS.API.Controllers.SystemConfigController",
        "SIS.API.Controllers.WebModuleSettingsController",
    ];
}
