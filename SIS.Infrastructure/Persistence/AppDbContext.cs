using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.Services;
using SIS.Domain.Entities;
using SIS.Domain.Identity;

namespace SIS.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    private readonly ICurrentUserScope _scope;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
        _scope = NoopCurrentUserScope.Instance;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserScope scope)
        : base(options)
    {
        _scope = scope;
    }

    // DbSets
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentPersonalInfo> StudentPersonalInfos => Set<StudentPersonalInfo>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<StudentDocument> StudentDocuments => Set<StudentDocument>();
    public DbSet<IssuedDocument> IssuedDocuments => Set<IssuedDocument>();
    public DbSet<StudentScholarshipDetail> StudentScholarshipDetails => Set<StudentScholarshipDetail>();
    public DbSet<StudentPlacement> StudentPlacements => Set<StudentPlacement>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<AcademicCvActivity> AcademicCvActivities => Set<AcademicCvActivity>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AcademicProgram> AcademicPrograms => Set<AcademicProgram>();
    public DbSet<Curriculum> Curricula => Set<Curriculum>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CurriculumCourse> CurriculumCourses => Set<CurriculumCourse>();
    public DbSet<CoursePrerequisite> CoursePrerequisites => Set<CoursePrerequisite>();
    public DbSet<CourseEquivalency> CourseEquivalencies => Set<CourseEquivalency>();
    public DbSet<CourseSection> CourseSections => Set<CourseSection>();
    public DbSet<CourseRegistration> CourseRegistrations => Set<CourseRegistration>();
    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<TermAttendancePolicy> TermAttendancePolicies => Set<TermAttendancePolicy>();
    public DbSet<StudentProgram> StudentPrograms => Set<StudentProgram>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<TermAcademicCalendar> TermAcademicCalendars => Set<TermAcademicCalendar>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
public DbSet<GradingCriteria> GradingCriteria => Set<GradingCriteria>();
    public DbSet<GradeComponent> GradeComponents => Set<GradeComponent>();
    public DbSet<StudentGrade> StudentGrades => Set<StudentGrade>();
    public DbSet<CourseSectionApproval> CourseSectionApprovals => Set<CourseSectionApproval>();
    public DbSet<SchedulingAuditLog> SchedulingAuditLogs => Set<SchedulingAuditLog>();
    public DbSet<TermCalendarEvent> TermCalendarEvents => Set<TermCalendarEvent>();
    public DbSet<CalendarTemplate> CalendarTemplates => Set<CalendarTemplate>();
    public DbSet<HolidayDefinition> HolidayDefinitions => Set<HolidayDefinition>();
    public DbSet<CalendarConstraint> CalendarConstraints => Set<CalendarConstraint>();
    public DbSet<GeneratedCalendarDraft> GeneratedCalendarDrafts => Set<GeneratedCalendarDraft>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<SectionSchedule> SectionSchedules => Set<SectionSchedule>();
    public DbSet<ScheduleImportSession> ScheduleImportSessions => Set<ScheduleImportSession>();
    public DbSet<ClassroomTermAssignment> ClassroomTermAssignments => Set<ClassroomTermAssignment>();
    public DbSet<CourseAccessPolicy> CourseAccessPolicies => Set<CourseAccessPolicy>();
    public DbSet<RegistrationPlan> RegistrationPlans => Set<RegistrationPlan>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<WebModuleSetting> WebModuleSettings => Set<WebModuleSetting>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<PromotionRule> PromotionRules => Set<PromotionRule>();
    public DbSet<ExamRegulation> ExamRegulations => Set<ExamRegulation>();
    public DbSet<SectionExam> SectionExams => Set<SectionExam>();
    public DbSet<SectionExamClassroom> SectionExamClassrooms => Set<SectionExamClassroom>();
    public DbSet<FacultyScheduleApproval> FacultyScheduleApprovals => Set<FacultyScheduleApproval>();
    public DbSet<ConstraintDefinition> ConstraintDefinitions => Set<ConstraintDefinition>();
    public DbSet<ConstraintException> ConstraintExceptions => Set<ConstraintException>();
    public DbSet<ElectiveGroup> ElectiveGroups => Set<ElectiveGroup>();
    public DbSet<ElectiveGroupCourse> ElectiveGroupCourses => Set<ElectiveGroupCourse>();
    public DbSet<ElectiveSubtypeEntity> ElectiveSubtypes => Set<ElectiveSubtypeEntity>();
    public DbSet<CurriculumCourseOpeningReason> CurriculumCourseOpeningReasons => Set<CurriculumCourseOpeningReason>();
    public DbSet<CourseCategory> CourseCategories => Set<CourseCategory>();
    public DbSet<CurriculumElectivePool> CurriculumElectivePools => Set<CurriculumElectivePool>();
    public DbSet<CurriculumTermRule> CurriculumTermRules => Set<CurriculumTermRule>();
    public DbSet<CourseAccessRule> CourseAccessRules => Set<CourseAccessRule>();
    public DbSet<ForecastCalibration> ForecastCalibrations => Set<ForecastCalibration>();
    public DbSet<ScheduleRun> ScheduleRuns => Set<ScheduleRun>();
    public DbSet<EvaluationPeriod> EvaluationPeriods => Set<EvaluationPeriod>();
    public DbSet<EvaluationQuestion> EvaluationQuestions => Set<EvaluationQuestion>();
    public DbSet<EvaluationPeriodQuestion> EvaluationPeriodQuestions => Set<EvaluationPeriodQuestion>();
    public DbSet<EvaluationResponse> EvaluationResponses => Set<EvaluationResponse>();
    public DbSet<EvaluationAnswer> EvaluationAnswers => Set<EvaluationAnswer>();
    public DbSet<InternalAnnouncement> InternalAnnouncements => Set<InternalAnnouncement>();
    public DbSet<AnnouncementRead> AnnouncementReads => Set<AnnouncementRead>();
    public DbSet<AnnouncementCategory> AnnouncementCategories => Set<AnnouncementCategory>();
    public DbSet<AnnouncementAttachment> AnnouncementAttachments => Set<AnnouncementAttachment>();
    public DbSet<DistributionConstraint> DistributionConstraints => Set<DistributionConstraint>();
    public DbSet<DistributionConstraintCourse> DistributionConstraintCourses => Set<DistributionConstraintCourse>();
    public DbSet<ConstraintExceptionRequest> ConstraintExceptionRequests => Set<ConstraintExceptionRequest>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<AdvisorCourseConstraint> AdvisorCourseConstraints => Set<AdvisorCourseConstraint>();
    public DbSet<CourseCrossList> CourseCrossLists => Set<CourseCrossList>();

    // ── RBAC (Phase 1) ──────────────────────────────────────────
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<MenuModule> MenuModules => Set<MenuModule>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuItemPermission> MenuItemPermissions => Set<MenuItemPermission>();

    // ── RBAC (Phase 2) ──────────────────────────────────────────
    public DbSet<PermissionBundle> PermissionBundles => Set<PermissionBundle>();
    public DbSet<PermissionBundleItem> PermissionBundleItems => Set<PermissionBundleItem>();
    public DbSet<UserScope> UserScopes => Set<UserScope>();
    public DbSet<PermissionDelegation> PermissionDelegations => Set<PermissionDelegation>();

    // ── RBAC (Phase 3 — Report Authorization) ───────────────────
    public DbSet<ReportCategory> ReportCategories => Set<ReportCategory>();
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<ReportUserGroup> ReportUserGroups => Set<ReportUserGroup>();
    public DbSet<ReportUserGroupMember> ReportUserGroupMembers => Set<ReportUserGroupMember>();
    public DbSet<ReportAccessGrant> ReportAccessGrants => Set<ReportAccessGrant>();

    // ── Sistem kataloğu (Adım 3) ──
    public DbSet<Katalog.KatalogKaydi> KatalogKayitlari => Set<Katalog.KatalogKaydi>();
    public DbSet<Katalog.KatalogAdimi> KatalogAdimlari => Set<Katalog.KatalogAdimi>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables

        builder.Entity<Katalog.KatalogKaydi>(e =>
        {
            e.ToTable("KatalogKayitlari");
            e.Property(k => k.Tur).HasMaxLength(50).IsRequired();
            e.Property(k => k.Anahtar).HasMaxLength(200).IsRequired();
            e.HasIndex(k => new { k.Tur, k.Anahtar }).IsUnique();
        });
        builder.Entity<Katalog.KatalogAdimi>(e =>
        {
            e.ToTable("KatalogAdimlari");
            e.Property(a => a.Ad).HasMaxLength(200).IsRequired();
            e.HasIndex(a => a.Ad).IsUnique();
        });

        // ── Student ──────────────────────────────────────────────
        builder.Entity<Student>(e =>
        {
            e.HasIndex(s => s.StudentNumber).IsUnique();
            e.HasIndex(s => s.NationalId).IsUnique();
            e.HasIndex(s => s.UserId).IsUnique();
            e.Property(s => s.Cgpa).HasPrecision(4, 2);
            e.Property(s => s.Sgpa).HasPrecision(4, 2);
            e.Property(s => s.PreviousStudentNumber).HasMaxLength(40);

            e.HasOne<ApplicationUser>()
             .WithOne()
             .HasForeignKey<Student>(s => s.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Advisor)
             .WithMany()
             .HasForeignKey(s => s.AdvisorId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── StudentPersonalInfo (1-1 Student) ────────────────────
        builder.Entity<StudentPersonalInfo>(e =>
        {
            e.HasOne<Student>()
             .WithOne(s => s.PersonalInfo)
             .HasForeignKey<StudentPersonalInfo>(p => p.StudentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => p.StudentId).IsUnique();

            // Contact
            e.Property(p => p.MobilePhone).HasMaxLength(40);
            e.Property(p => p.Phone).HasMaxLength(40);
            e.Property(p => p.SecondaryEmail).HasMaxLength(120);
            e.Property(p => p.AddressProvince).HasMaxLength(120);
            e.Property(p => p.AddressDistrict).HasMaxLength(120);
            e.Property(p => p.AddressLine).HasMaxLength(250);
            e.Property(p => p.PostalCode).HasMaxLength(40);
            e.Property(p => p.EmergencyContactName).HasMaxLength(120);
            e.Property(p => p.EmergencyContactPhone).HasMaxLength(40);
            e.Property(p => p.EmergencyContactRelation).HasMaxLength(120);

            // Demographic
            e.Property(p => p.BirthPlace).HasMaxLength(120);
            e.Property(p => p.Nationality).HasMaxLength(120);
            e.Property(p => p.BloodType).HasMaxLength(10);
            e.Property(p => p.PassportNo).HasMaxLength(40);

            // Military
            e.Property(p => p.MilitaryOffice).HasMaxLength(120);

            // Photo
            e.Property(p => p.PhotoExtension).HasMaxLength(10);
        });

        // ── DocumentType (seeded reference catalog) ──────────────
        builder.Entity<DocumentType>(e =>
        {
            e.HasIndex(d => d.Code).IsUnique();
            e.Property(d => d.Code).HasMaxLength(40);
            e.Property(d => d.Name).HasMaxLength(160);
        });

        // ── StudentDocument (per-student uploaded docs, ≤1 per type) ──
        builder.Entity<StudentDocument>(e =>
        {
            e.HasOne<Student>()
             .WithMany(s => s.Documents)
             .HasForeignKey(d => d.StudentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<DocumentType>()
             .WithMany()
             .HasForeignKey(d => d.DocumentTypeId)
             .OnDelete(DeleteBehavior.Restrict);

            // At most one document per (student, type).
            e.HasIndex(d => new { d.StudentId, d.DocumentTypeId }).IsUnique();

            e.Property(d => d.FileExtension).HasMaxLength(10);
            e.Property(d => d.OriginalFileName).HasMaxLength(260);
        });

        // ── IssuedDocument (append-only log of generated documents) ──
        builder.Entity<IssuedDocument>(e =>
        {
            e.HasOne<Student>()
             .WithMany(s => s.IssuedDocuments)
             .HasForeignKey(d => d.StudentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(d => new { d.StudentId, d.IssuedAt });

            e.Property(d => d.DocumentNumber).HasMaxLength(40);
        });

        // ── StudentScholarshipDetail (1-to-many child of Student) ──
        builder.Entity<StudentScholarshipDetail>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint(
                "CK_StudentScholarshipDetail_RatePercent",
                "\"RatePercent\" IS NULL OR (\"RatePercent\" >= 0 AND \"RatePercent\" <= 100)"));

            e.HasOne<Student>()
             .WithMany(s => s.ScholarshipDetails)
             .HasForeignKey(d => d.StudentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => new { d.StudentId, d.IsActive });
            e.Property(d => d.RatePercent).HasPrecision(5, 2);
            e.Property(d => d.Source).HasMaxLength(160);
            e.Property(d => d.Note).HasMaxLength(500);
        });

        builder.Entity<StudentPlacement>(e =>
        {
            e.HasOne<Student>()
             .WithOne(s => s.Placement)
             .HasForeignKey<StudentPlacement>(p => p.StudentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => p.StudentId).IsUnique();
            e.Property(p => p.Score).HasPrecision(9, 4);
            e.Property(p => p.ScoreType).HasMaxLength(20);
            e.Property(p => p.QuotaType).HasMaxLength(80);
            e.Property(p => p.PlacedProgramName).HasMaxLength(200);
            e.Property(p => p.Note).HasMaxLength(500);
        });

        // ── Instructor ───────────────────────────────────────────
        builder.Entity<Instructor>(e =>
        {
            e.HasIndex(i => i.UserId).IsUnique();

            e.HasOne<ApplicationUser>()
             .WithOne()
             .HasForeignKey<Instructor>(i => i.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(i => i.Department)
             .WithMany()
             .HasForeignKey(i => i.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AcademicCvActivity>(e =>
        {
            e.Property(a => a.Title).IsRequired().HasMaxLength(200);
            e.Property(a => a.Description).HasMaxLength(2000);
            e.HasIndex(a => new { a.InstructorId, a.ActivityYear });

            e.HasOne(a => a.Instructor)
             .WithMany(i => i.AcademicCvActivities)
             .HasForeignKey(a => a.InstructorId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Faculty ─────────────────────────────────────────────
        builder.Entity<Faculty>(e =>
        {
            e.HasIndex(f => f.Code).IsUnique();
        });

        // ── Department ───────────────────────────────────────────
        builder.Entity<Department>(e =>
        {
            e.HasIndex(d => d.Code).IsUnique();

            e.HasOne(d => d.Faculty)
             .WithMany(f => f.Departments)
             .HasForeignKey(d => d.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AcademicProgram ──────────────────────────────────────
        builder.Entity<AcademicProgram>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.HasIndex(p => p.ExternalSystemCode)
                .IsUnique()
                .HasFilter("\"ExternalSystemCode\" IS NOT NULL");
            e.Property(p => p.ExternalSystemCode).HasMaxLength(30);

            e.HasOne(p => p.Department)
             .WithMany(d => d.Programs)
             .HasForeignKey(p => p.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Curriculum ───────────────────────────────────────────
        builder.Entity<Curriculum>(e =>
        {
            e.HasOne(c => c.Program)
             .WithMany(p => p.Curricula)
             .HasForeignKey(c => c.ProgramId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Course ───────────────────────────────────────────────
        builder.Entity<Course>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();

            e.HasOne(c => c.Department)
             .WithMany()
             .HasForeignKey(c => c.DepartmentId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── CourseSection.CrossListGroupId — index for group lookup ──
        builder.Entity<CourseSection>(e =>
        {
            e.HasIndex(cs => cs.CrossListGroupId);
            e.Property(cs => cs.IsExamCoordinator).HasDefaultValue(false);
        });

        // ── CurriculumCourse (composite PK) ─────────────────────
        builder.Entity<CurriculumCourse>(e =>
        {
            e.HasKey(cc => new { cc.CurriculumId, cc.CourseId });

            e.HasOne(cc => cc.Curriculum)
             .WithMany(c => c.CurriculumCourses)
             .HasForeignKey(cc => cc.CurriculumId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cc => cc.Course)
             .WithMany()
             .HasForeignKey(cc => cc.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            // Faz D D1: ElectiveGroup FK kaldırıldı (ölü FK — hiçbir code path set etmiyordu).
            // Yeni model: pool/slot tabanlı CurriculumElectivePool (B1+).

            e.HasOne(cc => cc.OpeningReason)
             .WithMany()
             .HasForeignKey(cc => cc.OpeningReasonId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(cc => cc.Category)
             .WithMany()
             .HasForeignKey(cc => cc.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);

            // Phase 3 — İntibak (predecessor course); restrict delete to keep historical link safe
            e.HasOne(cc => cc.PreviousCourse)
             .WithMany()
             .HasForeignKey(cc => cc.PreviousCourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(cc => cc.PreviousCourseId);
        });

        // ── CurriculumCourseOpeningReason (lookup) ───────────────
        builder.Entity<CurriculumCourseOpeningReason>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Code).IsRequired().HasMaxLength(64);
            e.Property(r => r.Name).IsRequired().HasMaxLength(128);
            e.Property(r => r.Description).HasMaxLength(512);
            e.HasIndex(r => r.Code).IsUnique();
        });

        // ── CourseCategory (lookup) ──────────────────────────────
        builder.Entity<CourseCategory>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Code).IsRequired().HasMaxLength(64);
            e.Property(c => c.Name).IsRequired().HasMaxLength(128);
            e.HasIndex(c => c.Code).IsUnique();
        });

        // ── CoursePrerequisite (composite PK) ────────────────────
        builder.Entity<CoursePrerequisite>(e =>
        {
            e.HasKey(cp => new { cp.CourseId, cp.RequiredCourseId });

            e.HasOne(cp => cp.Course)
             .WithMany(c => c.Prerequisites)
             .HasForeignKey(cp => cp.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cp => cp.RequiredCourse)
             .WithMany()
             .HasForeignKey(cp => cp.RequiredCourseId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CourseEquivalency (composite PK) ─────────────────────
        builder.Entity<CourseEquivalency>(e =>
        {
            e.HasKey(ce => new { ce.CourseId, ce.EquivalentCourseId });

            e.HasOne(ce => ce.Course)
             .WithMany(c => c.Equivalencies)
             .HasForeignKey(ce => ce.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ce => ce.EquivalentCourse)
             .WithMany()
             .HasForeignKey(ce => ce.EquivalentCourseId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ElectiveGroup ────────────────────────────────────────
        builder.Entity<ElectiveGroup>(e =>
        {
            e.HasIndex(eg => eg.Code).IsUnique();

            e.HasOne(eg => eg.Faculty)
             .WithMany()
             .HasForeignKey(eg => eg.FacultyId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(eg => eg.Department)
             .WithMany()
             .HasForeignKey(eg => eg.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(eg => eg.DepartmentId);

            // Faz B B3a: DB-driven ElectiveSubtypeEntity FK (nullable — geçişte).
            e.HasOne<ElectiveSubtypeEntity>()
             .WithMany()
             .HasForeignKey(eg => eg.ElectiveSubtypeId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(eg => eg.ElectiveSubtypeId);
        });

        // ── ElectiveSubtypeEntity (DB-driven lookup) ────────────
        builder.Entity<ElectiveSubtypeEntity>(entity =>
        {
            entity.ToTable("ElectiveSubtypes");
            entity.HasIndex(s => s.Code).IsUnique();
            entity.Property(s => s.Code).HasMaxLength(50).IsRequired();
            entity.Property(s => s.Name).HasMaxLength(100).IsRequired();
        });

        // ── ElectiveGroupCourse (composite PK) ──────────────────
        builder.Entity<ElectiveGroupCourse>(e =>
        {
            e.HasKey(egc => new { egc.ElectiveGroupId, egc.CourseId });

            e.HasOne(egc => egc.ElectiveGroup)
             .WithMany(eg => eg.Courses)
             .HasForeignKey(egc => egc.ElectiveGroupId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(egc => egc.Course)
             .WithMany()
             .HasForeignKey(egc => egc.CourseId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CurriculumElectivePool ──────────────────────────────
        builder.Entity<CurriculumElectivePool>(e =>
        {
            e.Property(cep => cep.SlotName).HasMaxLength(50).IsRequired();

            e.HasIndex(cep => new { cep.CurriculumId, cep.ElectiveGroupId, cep.SlotName }).IsUnique();

            e.HasOne(cep => cep.Curriculum)
             .WithMany(c => c.ElectivePools)
             .HasForeignKey(cep => cep.CurriculumId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(cep => cep.ElectiveGroup)
             .WithMany(eg => eg.CurriculumPools)
             .HasForeignKey(cep => cep.ElectiveGroupId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CurriculumTermRule (Müfredat Dönem Kuralları) ───────
        builder.Entity<CurriculumTermRule>(e =>
        {
            // Aynı (Curriculum, Sınıf, Dönem, Kural Tipi) kombinasyonu benzersizdir.
            e.HasIndex(r => new { r.CurriculumId, r.ClassYear, r.TermHalf, r.RuleType }).IsUnique();

            e.Property(r => r.Value).HasColumnType("numeric(10,2)");

            e.HasOne(r => r.Curriculum)
             .WithMany(c => c.TermRules)
             .HasForeignKey(r => r.CurriculumId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── CourseAccessRule ────────────────────────────────────
        builder.Entity<CourseAccessRule>(e =>
        {
            e.HasIndex(car => car.CourseId);

            e.Property(car => car.MinCgpa).HasPrecision(4, 2);

            e.HasOne(car => car.Course)
             .WithMany()
             .HasForeignKey(car => car.CourseId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(car => car.AllowedFaculty)
             .WithMany()
             .HasForeignKey(car => car.AllowedFacultyId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(car => car.AllowedDepartment)
             .WithMany()
             .HasForeignKey(car => car.AllowedDepartmentId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(car => car.AllowedProgram)
             .WithMany()
             .HasForeignKey(car => car.AllowedProgramId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── StudentProgram (composite PK) ────────────────────────
        builder.Entity<StudentProgram>(e =>
        {
            e.HasKey(sp => new { sp.StudentId, sp.ProgramId });

            e.HasOne(sp => sp.Student)
             .WithMany(s => s.StudentPrograms)
             .HasForeignKey(sp => sp.StudentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(sp => sp.Program)
             .WithMany()
             .HasForeignKey(sp => sp.ProgramId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(sp => sp.Curriculum)
             .WithMany()
             .HasForeignKey(sp => sp.CurriculumId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CourseSection ────────────────────────────────────────
        builder.Entity<CourseSection>(e =>
        {
            e.HasIndex(cs => new { cs.CourseId, cs.TermId, cs.SectionNumber }).IsUnique();
            e.HasIndex(cs => new { cs.CourseId, cs.TermId });

            // DB-level son savunma: koltuk sayısı negatife düşemez (uygulama katmanına ek güvence).
            e.ToTable(t => t.HasCheckConstraint("CK_CourseSection_EnrolledCount_NonNegative", "\"EnrolledCount\" >= 0"));

            e.HasOne(cs => cs.Course)
             .WithMany()
             .HasForeignKey(cs => cs.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cs => cs.Term)
             .WithMany()
             .HasForeignKey(cs => cs.TermId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cs => cs.Instructor)
             .WithMany(i => i.Sections)
             .HasForeignKey(cs => cs.InstructorId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(cs => cs.Classroom)
             .WithMany()
             .HasForeignKey(cs => cs.ClassroomId)
             .OnDelete(DeleteBehavior.SetNull);

            e.Property(cs => cs.RowVersion)
             .IsRowVersion();
        });

        builder.Entity<AttendanceSession>(e =>
        {
            e.HasIndex(s => s.Token).IsUnique();
            e.HasIndex(s => new { s.CourseSectionId, s.WeekNumber }).IsUnique();
            e.Property(s => s.Token).HasMaxLength(64);
            e.Property(s => s.Topic).HasMaxLength(200);

            e.HasOne(s => s.CourseSection)
             .WithMany()
             .HasForeignKey(s => s.CourseSectionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.Instructor)
             .WithMany()
             .HasForeignKey(s => s.InstructorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AttendanceRecord>(e =>
        {
            e.HasIndex(r => new { r.AttendanceSessionId, r.StudentId }).IsUnique();
            e.HasIndex(r => r.CourseRegistrationId);
            e.HasIndex(r => r.StudentId);

            e.HasOne(r => r.AttendanceSession)
             .WithMany(s => s.Records)
             .HasForeignKey(r => r.AttendanceSessionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.CourseRegistration)
             .WithMany()
             .HasForeignKey(r => r.CourseRegistrationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Student)
             .WithMany()
             .HasForeignKey(r => r.StudentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── TermAttendancePolicy (dönem/program bazlı devamsızlık limitleri) ──
        builder.Entity<TermAttendancePolicy>(e =>
        {
            // Bir dönemde her program için en fazla bir override (ProgramId dolu).
            // PostgreSQL'de unique index NULL'ları distinct sayar; bu yüzden iki ayrı filtered
            // unique index kullanılır: biri program override'ları için, biri dönem geneli (NULL) için.
            e.HasIndex(p => new { p.TermId, p.ProgramId })
             .IsUnique()
             .HasFilter("\"ProgramId\" IS NOT NULL");
            // Bir dönemde en fazla bir "dönem geneli" (ProgramId == null) kayıt.
            e.HasIndex(p => p.TermId)
             .IsUnique()
             .HasFilter("\"ProgramId\" IS NULL");

            e.Property(p => p.TheoryAbsenceLimitPercent).HasPrecision(5, 2);
            e.Property(p => p.LabAbsenceLimitPercent).HasPrecision(5, 2);
            e.Property(p => p.Method).HasMaxLength(20).IsRequired();

            e.HasOne(p => p.Term)
             .WithMany()
             .HasForeignKey(p => p.TermId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Program)
             .WithMany()
             .HasForeignKey(p => p.ProgramId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── CourseRegistration ───────────────────────────────────
        builder.Entity<CourseRegistration>(e =>
        {
            e.HasIndex(cr => new { cr.StudentId, cr.CourseSectionId }).IsUnique();

            e.HasOne(cr => cr.Student)
             .WithMany(s => s.Registrations)
             .HasForeignKey(cr => cr.StudentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cr => cr.CourseSection)
             .WithMany(cs => cs.Registrations)
             .HasForeignKey(cr => cr.CourseSectionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── GradeComponent ─────────────────────────────────────
builder.Entity<GradeComponent>(e =>
{
    e.Property(gc => gc.Name)
     .HasMaxLength(100)
     .IsRequired();

    e.Property(gc => gc.ExamType)
     .HasMaxLength(40)
     .HasDefaultValue("Other")
     .IsRequired();

    e.Property(gc => gc.ShortName)
     .HasMaxLength(40)
     .HasDefaultValue("")
     .IsRequired();

    e.Property(gc => gc.Weight)
     .HasPrecision(5, 2);

    e.Property(gc => gc.MinimumScore)
     .HasPrecision(5, 2);

    e.Property(gc => gc.IsRequired)
     .HasDefaultValue(true);

    e.Property(gc => gc.RequiresAttendanceEligibility)
     .HasDefaultValue(false);

    e.HasOne(gc => gc.Course)
     .WithMany()
     .HasForeignKey(gc => gc.CourseId)
     .OnDelete(DeleteBehavior.Restrict);

    e.HasOne(gc => gc.AcademicTerm)
     .WithMany()
     .HasForeignKey(gc => gc.AcademicTermId)
     .OnDelete(DeleteBehavior.Restrict);

    e.HasOne(gc => gc.PrerequisiteGradeComponent)
     .WithMany(gc => gc.DependentComponents)
     .HasForeignKey(gc => gc.PrerequisiteGradeComponentId)
     .OnDelete(DeleteBehavior.Restrict);
});

        // ── SectionExam ─────────────────────────────────────────
        builder.Entity<SectionExam>(e =>
        {
            // Karar (2026-05-31): bir şubede bir sınav kalemi için TEK oturum.
            // Bütünleme/mazeret bugün skor-bazlı (StudentGrade.MakeupScore) — ayrı GradeComponent
            // YOK, dolayısıyla makeup için ayrı SectionExam takvimlendirme şu an kapsam dışı.
            // İleride makeup sınavı ayrı tarih/derslikle takvimlenecekse: SectionExam'e ExamKind
            // (Regular/Makeup) ayrımı eklenip bu unique index (..., ExamKind)'e genişletilir (kırıcı değil).
            e.HasIndex(se => new { se.CourseSectionId, se.GradeComponentId }).IsUnique();
            e.Property(se => se.Description).HasMaxLength(500);

            e.HasOne(se => se.CourseSection)
             .WithMany()
             .HasForeignKey(se => se.CourseSectionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(se => se.GradeComponent)
             .WithMany()
             .HasForeignKey(se => se.GradeComponentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── SectionExamClassroom ────────────────────────────────
        builder.Entity<SectionExamClassroom>(e =>
        {
            e.HasIndex(sec => new { sec.SectionExamId, sec.ClassroomId }).IsUnique();

            e.HasOne(sec => sec.SectionExam)
             .WithMany(se => se.Classrooms)
             .HasForeignKey(sec => sec.SectionExamId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(sec => sec.Classroom)
             .WithMany()
             .HasForeignKey(sec => sec.ClassroomId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── StudentGrade ──────────────────────────────────────
        builder.Entity<StudentGrade>(e =>
        {
            // Bir öğrenci bir bileşenden tek not alır
            e.HasIndex(sg => new { sg.CourseRegistrationId, sg.GradeComponentId }).IsUnique();

            e.Property(sg => sg.Score).HasPrecision(5, 2);
            e.Property(sg => sg.MakeupScore).HasPrecision(5, 2);

            e.HasOne(sg => sg.CourseRegistration)
             .WithMany()
             .HasForeignKey(sg => sg.CourseRegistrationId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(sg => sg.GradeComponent)
             .WithMany(gc => gc.StudentGrades)
             .HasForeignKey(sg => sg.GradeComponentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── GradingCriteria ─────────────────────────────────────
builder.Entity<GradingCriteria>(e =>
{
    // Bir ders bir dönemde tek aralık tanımına sahip olur
    e.HasIndex(gc => new { gc.CourseId, gc.AcademicTermId }).IsUnique();

    e.Property(gc => gc.CalculationMethod)
     .HasMaxLength(30)
     .HasDefaultValue("Absolute")
     .IsRequired();

    e.HasOne(gc => gc.Course)
     .WithMany()
     .HasForeignKey(gc => gc.CourseId)
     .OnDelete(DeleteBehavior.Restrict);

    e.HasOne(gc => gc.AcademicTerm)
     .WithMany()
     .HasForeignKey(gc => gc.AcademicTermId)
     .OnDelete(DeleteBehavior.Restrict);
});

        // ── CourseSectionApproval ──────────────────────────────
        builder.Entity<CourseSectionApproval>(e =>
        {
            e.HasIndex(a => new { a.CourseId, a.DepartmentId, a.TermId }).IsUnique();

            e.HasOne(a => a.Course)
             .WithMany()
             .HasForeignKey(a => a.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Department)
             .WithMany()
             .HasForeignKey(a => a.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Term)
             .WithMany()
             .HasForeignKey(a => a.TermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── SchedulingAuditLog ──────────────────────────────────
        builder.Entity<SchedulingAuditLog>(entity =>
        {
            entity.HasOne(e => e.Section)
                .WithMany()
                .HasForeignKey(e => e.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.SectionId, e.CreatedAt });
        });

        // ── AcademicTerm ───────────────────────────────────────
        builder.Entity<AcademicTerm>(e =>
        {
            // Varsayılan KAPALI: dönem oluşunca otomatik aktif olmaz; admin "Kullanıma Aç"
            // ile tek-aktif olarak açar (Proliz/Banner modeli).
            e.Property(t => t.IsActivated).HasDefaultValue(false);
            e.HasIndex(t => new { t.Year, t.TermType }).IsUnique();

            // Faz metotları (IsRegistrationOpen/AddDrop/Withdrawal/GradeEntry) AcademicCalendar
            // yüklenmemişse sessizce false döner — ".Include unutuldu" ile "takvim yok" ayırt edilemez.
            // AutoInclude her AcademicTerm sorgusunda takvimi otomatik yükleyerek bu tuzağı kapatır.
            // İstisnai durumda sorgu bazında .IgnoreAutoInclude() ile devre dışı bırakılabilir.
            e.Navigation(t => t.AcademicCalendar).AutoInclude();
        });

        // ── TermAcademicCalendar (1-1 AcademicTerm) ────────────
        builder.Entity<TermAcademicCalendar>(e =>
        {
            e.HasOne(c => c.Term)
             .WithOne(t => t.AcademicCalendar)
             .HasForeignKey<TermAcademicCalendar>(c => c.TermId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(c => c.TermId).IsUnique();
        });

        // ── TermCalendarEvent ──────────────────────────────────
        builder.Entity<TermCalendarEvent>(e =>
        {
            e.HasOne(ev => ev.Term)
             .WithMany(t => t.CalendarEvents)
             .HasForeignKey(ev => ev.TermId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(ev => new { ev.TermId, ev.StartDate });
            e.HasIndex(ev => new { ev.IsGenerated, ev.GeneratedDraftId });
        });

        // ── Notification (kişisel bildirim) ────────────────────
        builder.Entity<Notification>(e =>
        {
            e.Property(n => n.Title).HasMaxLength(200);
            e.Property(n => n.Body).HasMaxLength(2000);
            e.Property(n => n.Link).HasMaxLength(500);
            e.Property(n => n.EntityRef).HasMaxLength(120);
            e.Property(n => n.GroupKey).HasMaxLength(120);

            // Zil/sayfa sorguları: kullanıcının okunmamışları + en güncel önce.
            e.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
        });

        // ── Academic Calendar Generator ────────────────────────
        builder.Entity<CalendarTemplate>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(160);
            e.Property(t => t.ChainCode).HasMaxLength(40);
            e.Property(t => t.ItemsJson).HasColumnType("jsonb");
            e.HasIndex(t => t.ChainCode).IsUnique();
        });

        builder.Entity<HolidayDefinition>(e =>
        {
            e.Property(h => h.Name).HasMaxLength(160);
            e.Property(h => h.EnglishName).HasMaxLength(160);
            e.Property(h => h.Source).HasMaxLength(80);
            e.Property(h => h.DurationDays).HasPrecision(4, 1);
            e.Property(h => h.OverridesJson).HasColumnType("jsonb");
            e.HasIndex(h => h.Name).IsUnique();
        });

        builder.Entity<CalendarConstraint>(e =>
        {
            e.Property(c => c.AcademicYear).HasMaxLength(20);
            e.Property(c => c.TargetChainCode).HasMaxLength(40);
            e.Property(c => c.ParameterJson).HasColumnType("jsonb");
            e.HasIndex(c => new { c.AcademicYear, c.ConstraintType });
        });

        builder.Entity<GeneratedCalendarDraft>(e =>
        {
            e.Property(d => d.AcademicYear).HasMaxLength(20);
            e.Property(d => d.EventsJson).HasColumnType("jsonb");
            e.Property(d => d.HolidaysJson).HasColumnType("jsonb");
            e.Property(d => d.WarningsJson).HasColumnType("jsonb");
            e.Property(d => d.PreviousSnapshotJson).HasColumnType("jsonb");
            e.HasIndex(d => new { d.AcademicYear, d.Status });
            e.HasIndex(d => d.ExpiresAt);
        });

        // ── TimeSlot ─────────────────────────────────────────────
        builder.Entity<TimeSlot>(e =>
        {
            e.HasIndex(ts => new { ts.TermId, ts.DayOfWeek, ts.StartTime }).IsUnique();

            e.HasOne(ts => ts.Term)
             .WithMany()
             .HasForeignKey(ts => ts.TermId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── SectionSchedule ──────────────────────────────────────
        builder.Entity<SectionSchedule>(e =>
        {
            // Lab split desteği: aynı section + aynı slot, FARKLI derslik kombinasyonuna izin ver.
            // Kapasite yetmeyen lab oturumları 2+ oda'ya bölünebilir (ör. BM101 lab L139+L141 aynı anda).
            // ClassroomId'nin index'e dahil edilmesi (section, slot, classroom) üçlüsünü unique yapar.
            e.HasIndex(ss => new { ss.CourseSectionId, ss.TimeSlotId, ss.ClassroomId }).IsUnique();

            // Çift-rezervasyon koruması (DB seviyesi): aynı derslik + aynı zaman dilimi iki FARKLI
            // satırda olamaz → cross-section classroom double-booking fiziksel olarak engellenir.
            // Partial: ClassroomId IS NOT NULL (online dersler hariç). Lab-split bozulmaz (aynı slot
            // farklı oda = farklı ClassroomId). H1 ClassroomConflict kuralının DB karşılığı; SkipHard/
            // Force ile yazma yollarının arkasındaki tek güvenlik ağı.
            e.HasIndex(ss => new { ss.ClassroomId, ss.TimeSlotId })
             .IsUnique()
             .HasFilter("\"ClassroomId\" IS NOT NULL")
             .HasDatabaseName("IX_SectionSchedules_Classroom_TimeSlot_Unique");

            e.HasOne(ss => ss.CourseSection)
             .WithMany(cs => cs.SectionSchedules)
             .HasForeignKey(ss => ss.CourseSectionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ss => ss.TimeSlot)
             .WithMany()
             .HasForeignKey(ss => ss.TimeSlotId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ss => ss.Classroom)
             .WithMany()
             .HasForeignKey(ss => ss.ClassroomId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ClassroomTermAssignment ──────────────────────────────
        // Schedule import sessions keep server-validated import rows out of client-controlled apply requests.
        builder.Entity<ScheduleImportSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.RowsJson).HasColumnType("jsonb").IsRequired();
            e.HasIndex(s => new { s.CreatedByUserId, s.FacultyId, s.TermId, s.ExpiresAt });
            e.HasIndex(s => s.ConsumedAt);

            e.HasOne(s => s.Faculty)
             .WithMany()
             .HasForeignKey(s => s.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Term)
             .WithMany()
             .HasForeignKey(s => s.TermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClassroomTermAssignment>(e =>
        {
            e.HasIndex(cta => new { cta.TermId, cta.ClassroomId }).IsUnique();

            e.HasOne(cta => cta.Term)
             .WithMany()
             .HasForeignKey(cta => cta.TermId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cta => cta.Classroom)
             .WithMany()
             .HasForeignKey(cta => cta.ClassroomId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cta => cta.Faculty)
             .WithMany()
             .HasForeignKey(cta => cta.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CourseAccessPolicy ───────────────────────────────────
        builder.Entity<CourseAccessPolicy>(e =>
        {
            e.HasOne(cap => cap.Course)
             .WithMany()
             .HasForeignKey(cap => cap.CourseId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(cap => cap.Term)
             .WithMany()
             .HasForeignKey(cap => cap.TermId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cap => cap.Faculty)
             .WithMany()
             .HasForeignKey(cap => cap.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(cap => cap.Department)
             .WithMany()
             .HasForeignKey(cap => cap.DepartmentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RegistrationPlan ─────────────────────────────────────
        builder.Entity<RegistrationPlan>(e =>
        {
            e.HasIndex(rp => new { rp.StudentId, rp.TermId }).IsUnique();

            e.HasOne(rp => rp.Student)
             .WithMany()
             .HasForeignKey(rp => rp.StudentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(rp => rp.Term)
             .WithMany()
             .HasForeignKey(rp => rp.TermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AdvisorCourseConstraint ─────────────────────────────
        builder.Entity<AdvisorCourseConstraint>(e =>
        {
            e.HasIndex(ac => new { ac.StudentId, ac.CourseId, ac.TermId }).IsUnique();
            e.HasIndex(ac => new { ac.StudentId, ac.TermId, ac.IsActive });

            e.Property(ac => ac.Note).HasMaxLength(500);

            e.HasOne(ac => ac.Student)
             .WithMany()
             .HasForeignKey(ac => ac.StudentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ac => ac.Course)
             .WithMany()
             .HasForeignKey(ac => ac.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ac => ac.Term)
             .WithMany()
             .HasForeignKey(ac => ac.TermId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(ac => ac.Advisor)
             .WithMany()
             .HasForeignKey(ac => ac.AdvisorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CourseRegistration → RegistrationPlan FK ─────────────
        builder.Entity<CourseRegistration>(e2 =>
        {
            e2.HasOne(cr => cr.RegistrationPlan)
              .WithMany(rp => rp.Registrations)
              .HasForeignKey(cr => cr.RegistrationPlanId)
              .OnDelete(DeleteBehavior.SetNull);

            // Havuz snapshot FK (null = zorunlu veya tekil seçmeli)
            e2.HasOne(cr => cr.AppliedElectivePool)
              .WithMany()
              .HasForeignKey(cr => cr.AppliedElectivePoolId)
              .OnDelete(DeleteBehavior.SetNull);

            // Faz 4 — Açma Nedeni snapshot (null = legacy kayıt veya reason'suz ders)
            e2.HasOne(cr => cr.OpeningReasonSnapshot)
              .WithMany()
              .HasForeignKey(cr => cr.OpeningReasonIdSnapshot)
              .OnDelete(DeleteBehavior.SetNull);
        });

        // ── CourseCrossList (M:N Course ↔ Course) ───────────────
        builder.Entity<CourseCrossList>(e =>
        {
            e.HasKey(x => new { x.CourseId, x.LinkedCourseId });
            e.HasIndex(x => x.LinkedCourseId);

            e.HasOne(x => x.Course)
             .WithMany()
             .HasForeignKey(x => x.CourseId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.LinkedCourse)
             .WithMany()
             .HasForeignKey(x => x.LinkedCourseId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AuditLog ─────────────────────────────────────────────
        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(al => al.Timestamp);
            e.HasIndex(al => new { al.EntityType, al.EntityId });
            e.HasIndex(al => al.UserId);
            // Action filtre/sıralamada ve stats+anomali sayımlarında ağır kullanılıyor.
            // (Timestamp, Action) bileşik indeksi "son X saatte Action=Y" anomali sorgularını da kapsar.
            e.HasIndex(al => new { al.Timestamp, al.Action });
        });

        // ── SystemConfig ─────────────────────────────────────────
        builder.Entity<SystemConfig>(e =>
        {
            e.HasIndex(sc => sc.Key).IsUnique();
            e.Property(sc => sc.RowVersion)
             .IsRowVersion();
        });

        // ── WebModuleSetting ─────────────────────────────────────
        builder.Entity<WebModuleSetting>(e =>
        {
            e.HasIndex(wms => new { wms.ModuleKey, wms.Key }).IsUnique();
            e.Property(wms => wms.RowVersion)
             .IsRowVersion();
        });

        // ── UserPreference ───────────────────────────────────────
        builder.Entity<UserPreference>(e =>
        {
            e.HasIndex(up => up.UserId).IsUnique();
            e.Property(up => up.FavoritesJson).HasColumnType("jsonb");

            e.HasOne<ApplicationUser>()
             .WithOne()
             .HasForeignKey<UserPreference>(up => up.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── PromotionRule ────────────────────────────────────────
        builder.Entity<PromotionRule>(e =>
        {
            // γ hibrit unique: aynı program×müfredat×fromYear×toYear kombinasyonu tek olabilir.
            // CurriculumId null olabilir → program-bazlı default; set ise müfredat-spesifik override.
            e.HasIndex(pr => new { pr.ProgramId, pr.CurriculumId, pr.FromYear, pr.ToYear }).IsUnique();
            e.Property(pr => pr.MinCgpa).HasPrecision(4, 2);

            e.HasOne(pr => pr.Program)
             .WithMany()
             .HasForeignKey(pr => pr.ProgramId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(pr => pr.Curriculum)
             .WithMany()
             .HasForeignKey(pr => pr.CurriculumId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ExamRegulation ───────────────────────────────────────
        builder.Entity<ExamRegulation>(e =>
        {
            e.HasIndex(r => new { r.ProgramId, r.DegreeLevel, r.TermType, r.Year }).IsUnique();
            e.Property(r => r.Name).HasMaxLength(200).IsRequired();
            e.Property(r => r.MinMidtermWeightPercent).HasPrecision(5, 2);
            e.Property(r => r.MaxMidtermWeightPercent).HasPrecision(5, 2);
            e.Property(r => r.MinFinalWeightPercent).HasPrecision(5, 2);
            e.Property(r => r.MaxFinalWeightPercent).HasPrecision(5, 2);
            e.Property(r => r.PassingGrade).HasPrecision(5, 2);
            e.Property(r => r.RawScoreFloor1).HasPrecision(5, 2);
            e.Property(r => r.RawScoreFloor2).HasPrecision(5, 2);
            e.Property(r => r.FinalExamFloor).HasPrecision(5, 2);
            e.HasOne(r => r.Program).WithMany()
             .HasForeignKey(r => r.ProgramId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Classroom ────────────────────────────────────────────
        builder.Entity<Classroom>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique();
        });

        // ── FacultyScheduleApproval ──────────────────────────────
        builder.Entity<FacultyScheduleApproval>(e =>
        {
            e.HasIndex(fsa => new { fsa.FacultyId, fsa.TermId }).IsUnique();

            e.HasOne(fsa => fsa.Faculty)
             .WithMany()
             .HasForeignKey(fsa => fsa.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(fsa => fsa.Term)
             .WithMany()
             .HasForeignKey(fsa => fsa.TermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ConstraintDefinition ────────────────────────────────
        builder.Entity<ConstraintDefinition>(e =>
        {
            e.HasIndex(cd => cd.Code).IsUnique();
            e.Property(cd => cd.Code).HasMaxLength(10);
            e.Property(cd => cd.Name).HasMaxLength(100);
            e.Property(cd => cd.Category).HasMaxLength(10);
            e.Property(cd => cd.Scope).HasMaxLength(20);
        });

        // ── ConstraintException ─────────────────────────────────
        builder.Entity<ConstraintException>(e =>
        {
            e.HasIndex(ce => new { ce.ConstraintDefinitionId, ce.InstructorId, ce.DepartmentId, ce.CourseSectionId, ce.TermId });
            e.HasOne(ce => ce.ConstraintDefinition).WithMany(d => d.Exceptions).HasForeignKey(ce => ce.ConstraintDefinitionId);
            e.HasOne(ce => ce.Instructor).WithMany().HasForeignKey(ce => ce.InstructorId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ce => ce.Student).WithMany().HasForeignKey(ce => ce.StudentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ce => ce.Department).WithMany().HasForeignKey(ce => ce.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ce => ce.CourseSection).WithMany().HasForeignKey(ce => ce.CourseSectionId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ce => ce.Term).WithMany().HasForeignKey(ce => ce.TermId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── DistributionConstraint ──────────────────────────────
        builder.Entity<DistributionConstraint>(e =>
        {
            e.HasIndex(d => d.TermId);
            e.HasOne(d => d.Term).WithMany().HasForeignKey(d => d.TermId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Department).WithMany().HasForeignKey(d => d.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DistributionConstraintCourse>(e =>
        {
            e.HasIndex(c => new { c.DistributionConstraintId, c.CourseId, c.CourseSectionId });
            e.HasOne(c => c.DistributionConstraint).WithMany(d => d.Courses).HasForeignKey(c => c.DistributionConstraintId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Course).WithMany().HasForeignKey(c => c.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.CourseSection).WithMany().HasForeignKey(c => c.CourseSectionId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── ConstraintExceptionRequest ──────────────────────────
        builder.Entity<ConstraintExceptionRequest>(e =>
        {
            e.HasIndex(r => r.Status);
            e.HasOne(r => r.ConstraintDefinition).WithMany().HasForeignKey(r => r.ConstraintDefinitionId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── ApprovalStep ────────────────────────────────────────
        builder.Entity<ApprovalStep>(e =>
        {
            e.HasIndex(s => new { s.RequestId, s.StepOrder });
            e.HasOne(s => s.Request).WithMany(r => r.ApprovalSteps).HasForeignKey(s => s.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Performance Indexes ──────────────────────────────────
        builder.Entity<CourseRegistration>(e3 =>
        {
            e3.HasIndex(cr => cr.StudentId);
            e3.HasIndex(cr => cr.CourseSectionId);
        });

        builder.Entity<Student>(e2 =>
        {
            e2.HasIndex(s => s.AdvisorId);
        });

        builder.Entity<GradeComponent>(e2 =>
        {
            e2.HasIndex(gc => new { gc.CourseId, gc.AcademicTermId, gc.Name }).IsUnique();
        });

        builder.Entity<Curriculum>(e2 =>
        {
            e2.HasIndex(c => new { c.ProgramId, c.Version }).IsUnique();
        });

        // ── ForecastCalibration ──────────────────────────────────
        builder.Entity<ForecastCalibration>(entity =>
        {
            entity.HasOne(fc => fc.Course).WithMany().HasForeignKey(fc => fc.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(fc => fc.Term).WithMany().HasForeignKey(fc => fc.TermId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(fc => fc.Department).WithMany().HasForeignKey(fc => fc.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(fc => new { fc.CourseId, fc.TermId }).IsUnique();
        });

        // ── ScheduleRun ──────────────────────────────────────────
        builder.Entity<ScheduleRun>(entity =>
        {
            entity.HasOne(sr => sr.Faculty).WithMany().HasForeignKey(sr => sr.FacultyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(sr => sr.Term).WithMany().HasForeignKey(sr => sr.TermId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(sr => new { sr.FacultyId, sr.TermId, sr.CreatedAt });
        });

        // ── EvaluationPeriod ─────────────────────────────────────
        builder.Entity<EvaluationPeriod>(e =>
        {
            e.HasIndex(ep => ep.TermId).IsUnique();

            e.HasOne(ep => ep.Term)
             .WithMany()
             .HasForeignKey(ep => ep.TermId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── EvaluationQuestion ───────────────────────────────────
        builder.Entity<EvaluationQuestion>(e =>
        {
            e.Property(q => q.Text).HasMaxLength(500);
            e.HasIndex(q => new { q.Category, q.SortOrder });
        });

        // ── EvaluationPeriodQuestion (snapshot join) ─────────────
        builder.Entity<EvaluationPeriodQuestion>(e =>
        {
            e.HasKey(pq => new { pq.EvaluationPeriodId, pq.EvaluationQuestionId });

            e.HasOne(pq => pq.Period)
             .WithMany()
             .HasForeignKey(pq => pq.EvaluationPeriodId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(pq => pq.Question)
             .WithMany()
             .HasForeignKey(pq => pq.EvaluationQuestionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── EvaluationResponse ───────────────────────────────────
        builder.Entity<EvaluationResponse>(e =>
        {
            // One evaluation per (period, section, student)
            e.HasIndex(r => new { r.EvaluationPeriodId, r.CourseSectionId, r.StudentId }).IsUnique();
            e.HasIndex(r => r.CourseSectionId);

            e.HasOne(r => r.EvaluationPeriod)
             .WithMany()
             .HasForeignKey(r => r.EvaluationPeriodId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.CourseSection)
             .WithMany()
             .HasForeignKey(r => r.CourseSectionId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Student)
             .WithMany()
             .HasForeignKey(r => r.StudentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── EvaluationAnswer ─────────────────────────────────────
        builder.Entity<EvaluationAnswer>(e =>
        {
            e.Property(a => a.TextValue).HasMaxLength(2000);

            e.HasOne(a => a.Response)
             .WithMany(r => r.Answers)
             .HasForeignKey(a => a.ResponseId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Question)
             .WithMany()
             .HasForeignKey(a => a.QuestionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── InternalAnnouncement ─────────────────────────────────
        builder.Entity<InternalAnnouncement>(e =>
        {
            // Title / Content max uzunlukları uygulama katmanında (WebModuleSettings) sınırlanır.
            // DB tarafında geniş tutuluyor ki admin limiti artırırsa şema müdahalesi gerekmesin.
            e.Property(a => a.Title).HasMaxLength(500);
            e.Property(a => a.Content).HasColumnType("text");
            e.Property(a => a.ContentFormat).HasMaxLength(10).HasDefaultValue("plain");
            e.Property(a => a.CreatedBy).HasMaxLength(100);
            e.Property(a => a.CreatedByName).HasMaxLength(200);

            e.HasIndex(a => new { a.IsActive, a.StartsAt, a.ExpiresAt });
            e.HasIndex(a => a.TargetAudience);
            e.HasIndex(a => a.TargetStudentYear);

            e.HasOne(a => a.TargetDepartment)
             .WithMany()
             .HasForeignKey(a => a.TargetDepartmentId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(a => a.TargetFaculty)
             .WithMany()
             .HasForeignKey(a => a.TargetFacultyId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(a => a.TargetSection)
             .WithMany()
             .HasForeignKey(a => a.TargetSectionId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(a => a.Category)
             .WithMany()
             .HasForeignKey(a => a.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(a => a.CategoryId);
        });

        // ── AnnouncementCategory ─────────────────────────────────
        builder.Entity<AnnouncementCategory>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.ColorHex).HasMaxLength(7).IsRequired();
            e.Property(c => c.IconName).HasMaxLength(50);
            e.HasIndex(c => c.Name).IsUnique();
            e.HasIndex(c => c.SortOrder);
        });

        // ── AnnouncementAttachment ───────────────────────────────
        builder.Entity<AnnouncementAttachment>(e =>
        {
            e.Property(a => a.FileName).HasMaxLength(260).IsRequired();
            e.Property(a => a.StorageFileName).HasMaxLength(300).IsRequired();
            e.Property(a => a.MimeType).HasMaxLength(150).IsRequired();
            e.Property(a => a.UploadedBy).HasMaxLength(100).IsRequired();

            e.HasOne(a => a.Announcement)
             .WithMany(p => p.Attachments)
             .HasForeignKey(a => a.AnnouncementId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(a => a.AnnouncementId);
        });

        // ── AnnouncementRead (composite PK: AnnouncementId + UserId) ──────
        builder.Entity<AnnouncementRead>(e =>
        {
            e.HasKey(r => new { r.AnnouncementId, r.UserId });

            e.HasOne(r => r.Announcement)
             .WithMany()
             .HasForeignKey(r => r.AnnouncementId)
             .OnDelete(DeleteBehavior.Cascade);

            // Kullanıcının okunmamış duyurularını sorgularken UserId üzerinden index gerekli
            e.HasIndex(r => r.UserId);
        });

        // ── UserScope ────────────────────────────────────────────
        builder.Entity<UserScope>(e =>
        {
            e.ToTable("UserScopes", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_UserScopes_Kind_Faculty",
                    "(\"Kind\" = 1 AND \"FacultyId\" IS NOT NULL)");
            });

            e.HasKey(scope => scope.Id);
            e.Property(scope => scope.Kind).HasConversion<int>();

            e.HasOne<ApplicationUser>()
             .WithMany()
             .HasForeignKey(scope => scope.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<Faculty>()
             .WithMany()
             .HasForeignKey(scope => scope.FacultyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(scope => scope.UserId)
             .HasDatabaseName("IX_UserScopes_UserId");

            e.HasIndex(scope => new { scope.UserId, scope.Kind })
             .IsUnique()
             .HasDatabaseName("UQ_UserScopes_UserId_Kind");
        });

        // ── PermissionDelegation (Vekâlet) ───────────────────────
        builder.Entity<PermissionDelegation>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Reason).HasMaxLength(500).IsRequired();
            e.Property(d => d.RevokeReason).HasMaxLength(500);

            e.HasOne<ApplicationUser>()
             .WithMany()
             .HasForeignKey(d => d.DelegatorUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<ApplicationUser>()
             .WithMany()
             .HasForeignKey(d => d.DelegateeUserId)
             .OnDelete(DeleteBehavior.Restrict);

            // Audit kayıtlarını korumak için aktif zaman penceresi sorguları
            e.HasIndex(d => new { d.DelegateeUserId, d.RevokedAtUtc, d.StartsAtUtc, d.EndsAtUtc })
             .HasDatabaseName("IX_PermissionDelegations_Delegatee_Active");
            e.HasIndex(d => new { d.DelegatorUserId, d.StartsAtUtc, d.EndsAtUtc })
             .HasDatabaseName("IX_PermissionDelegations_Delegator");
        });

        // ── RBAC Phase 3: ReportCategory ────────────────────────
        builder.Entity<ReportCategory>(e =>
        {
            e.Property(c => c.Code).HasMaxLength(64).IsRequired();
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(c => c.Code).IsUnique();
        });

        // ── RBAC Phase 3: ReportDefinition ──────────────────────
        builder.Entity<ReportDefinition>(e =>
        {
            e.Property(r => r.Code).HasMaxLength(100).IsRequired();
            e.Property(r => r.Name).HasMaxLength(200).IsRequired();
            e.Property(r => r.Description).HasMaxLength(500);
            e.HasIndex(r => r.Code).IsUnique();

            e.HasOne(r => r.Category)
             .WithMany(c => c.Reports)
             .HasForeignKey(r => r.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RBAC Phase 3 (Faz 2): ReportUserGroup ───────────────
        builder.Entity<ReportUserGroup>(e =>
        {
            e.Property(g => g.Name).HasMaxLength(200).IsRequired();
            e.Property(g => g.Description).HasMaxLength(500);
            e.HasIndex(g => g.Name).IsUnique();
        });

        builder.Entity<ReportUserGroupMember>(e =>
        {
            e.HasKey(m => new { m.GroupId, m.UserId });
            e.HasOne(m => m.Group)
             .WithMany(g => g.Members)
             .HasForeignKey(m => m.GroupId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => m.UserId);
        });

        // ── RBAC Phase 3 (Faz 2): ReportAccessGrant (birleşik) ──
        builder.Entity<ReportAccessGrant>(e =>
        {
            e.Property(g => g.Reason).HasMaxLength(500);
            e.Property(g => g.ScopeKind).HasConversion<int>();
            e.Property(g => g.SubjectKind).HasConversion<int>();
            e.Property(g => g.Effect).HasConversion<int>();
            // Aynı (hedef, özne) için tek satır → upsert
            e.HasIndex(g => new { g.ScopeKind, g.TargetId, g.SubjectKind, g.SubjectId }).IsUnique();
            e.HasIndex(g => new { g.SubjectKind, g.SubjectId });
        });

        // ── Permission Scope Query Filters (Phase 2) ─────────────
        // _scope.FacultyIds: kullanıcının erişebildiği fakülteler (kendi + vekâletten devralınan birleşim).
        builder.Entity<Faculty>().HasQueryFilter(faculty =>
            !_scope.IsActive || _scope.FacultyIds.Contains(faculty.Id));

        builder.Entity<Department>().HasQueryFilter(department =>
            !_scope.IsActive || _scope.FacultyIds.Contains(department.FacultyId));

        builder.Entity<AcademicProgram>().HasQueryFilter(program =>
            !_scope.IsActive || _scope.FacultyIds.Contains(program.Department.FacultyId));

        builder.Entity<Curriculum>().HasQueryFilter(curriculum =>
            !_scope.IsActive || _scope.FacultyIds.Contains(curriculum.Program.Department.FacultyId));

        builder.Entity<Course>().HasQueryFilter(course =>
            !_scope.IsActive || (course.Department != null && _scope.FacultyIds.Contains(course.Department.FacultyId)));

        builder.Entity<CourseSection>().HasQueryFilter(section =>
            !_scope.IsActive || (section.Course.Department != null && _scope.FacultyIds.Contains(section.Course.Department.FacultyId)));

        builder.Entity<Student>().HasQueryFilter(student =>
            !_scope.IsActive || student.StudentPrograms.Any(program => _scope.FacultyIds.Contains(program.Program.Department.FacultyId)));

        builder.Entity<Instructor>().HasQueryFilter(instructor =>
            !_scope.IsActive || _scope.FacultyIds.Contains(instructor.Department.FacultyId));

        builder.Entity<ElectiveGroup>().HasQueryFilter(group =>
            !_scope.IsActive
            || (group.DepartmentId == null && group.FacultyId == null)
            || (group.DepartmentId != null && _scope.FacultyIds.Contains(group.Department!.FacultyId))
            || (group.DepartmentId == null && group.FacultyId != null && _scope.FacultyIds.Contains(group.FacultyId.Value)));

        builder.Entity<CurriculumElectivePool>().HasQueryFilter(pool =>
            !_scope.IsActive || _scope.FacultyIds.Contains(pool.Curriculum.Program.Department.FacultyId));

        // ── RBAC: Permission ────────────────────────────────────
        builder.Entity<Permission>(e =>
        {
            e.Property(p => p.Key).HasMaxLength(150).IsRequired();
            e.Property(p => p.Description).HasMaxLength(500);
            e.HasIndex(p => p.Key).IsUnique();
            e.HasIndex(p => p.Category);
        });

        // ── RBAC: RolePermission (Phase 2 — surrogate PK + Source tracking) ─
        // Aynı (RoleId, PermissionId) için iki satır olabilir: biri Manual,
        // diğer(ler)i farklı bundle'lardan. Source ayrımı için surrogate Id PK kullanılır.
        // Filtered unique index'lerle veri bütünlüğü korunur.
        builder.Entity<RolePermission>(e =>
        {
            e.HasKey(rp => rp.Id);
            e.HasOne<IdentityRole<int>>()
             .WithMany()
             .HasForeignKey(rp => rp.RoleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rp => rp.Permission)
             .WithMany(p => p.RolePermissions)
             .HasForeignKey(rp => rp.PermissionId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rp => rp.SourceBundle)
             .WithMany(b => b.AppliedRolePermissions)
             .HasForeignKey(rp => rp.SourceBundleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(rp => rp.PermissionId);
            e.HasIndex(rp => rp.RoleId);
            // Manuel atamada her (rol, permission) tekildir
            e.HasIndex(rp => new { rp.RoleId, rp.PermissionId })
             .IsUnique()
             .HasFilter("\"SourceBundleId\" IS NULL")
             .HasDatabaseName("ix_role_permissions_manual_unique");
            // Bundle atamasında her (rol, permission, bundle) tekildir
            e.HasIndex(rp => new { rp.RoleId, rp.PermissionId, rp.SourceBundleId })
             .IsUnique()
             .HasFilter("\"SourceBundleId\" IS NOT NULL")
             .HasDatabaseName("ix_role_permissions_bundle_unique");
        });

        // ── RBAC Phase 2: PermissionBundle ──────────────────────
        builder.Entity<PermissionBundle>(e =>
        {
            e.Property(b => b.Name).HasMaxLength(200).IsRequired();
            e.Property(b => b.Description).HasMaxLength(1000);
            e.Property(b => b.Category).HasMaxLength(100);
            e.HasIndex(b => b.Name).IsUnique();
            e.HasIndex(b => b.Category);
        });

        // ── RBAC Phase 2: PermissionBundleItem (composite PK) ───
        builder.Entity<PermissionBundleItem>(e =>
        {
            e.HasKey(i => new { i.BundleId, i.PermissionId });
            e.HasOne(i => i.Bundle)
             .WithMany(b => b.Items)
             .HasForeignKey(i => i.BundleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Permission)
             .WithMany(p => p.BundleItems)
             .HasForeignKey(i => i.PermissionId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(i => i.PermissionId);
        });

        // ── RBAC: MenuModule (recursive tree) ───────────────────
        builder.Entity<MenuModule>(e =>
        {
            e.Property(m => m.Slug).HasMaxLength(100).IsRequired();
            e.Property(m => m.Label).HasMaxLength(100).IsRequired();
            e.Property(m => m.Icon).HasMaxLength(50);
            e.Property(m => m.AllowedRolesCsv).HasMaxLength(200);
            e.HasOne(m => m.Parent)
             .WithMany(m => m.Children)
             .HasForeignKey(m => m.ParentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => m.Slug).IsUnique();
            e.HasIndex(m => m.ParentId);
            e.HasIndex(m => new { m.ParentId, m.SortOrder });
        });

        // ── RBAC: MenuItem ──────────────────────────────────────
        // Route UNIQUE kaldırıldı — aynı route farklı modüllerde olabilir (örn. "Duyurular"
        // hem "Genel İşlemler" hem "Duyuru Yönetimi" altında). Composite (ModuleId, Route)
        // UNIQUE: aynı modülde aynı route iki kez yasak.
        builder.Entity<MenuItem>(e =>
        {
            e.Property(mi => mi.Label).HasMaxLength(150).IsRequired();
            e.Property(mi => mi.Route).HasMaxLength(200).IsRequired();
            e.Property(mi => mi.Slug).HasMaxLength(100).IsRequired();
            e.HasOne(mi => mi.Module)
             .WithMany(m => m.Items)
             .HasForeignKey(mi => mi.ModuleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(mi => mi.Route);
            // Katalog menü öğesini slug'la eşler (Adım 3.3a).
            e.HasIndex(mi => mi.Slug).IsUnique();
            e.HasIndex(mi => new { mi.ModuleId, mi.Route }).IsUnique();
            e.HasIndex(mi => new { mi.ModuleId, mi.SortOrder });
        });

        // ── RBAC: MenuItemPermission (composite PK) ─────────────
        builder.Entity<MenuItemPermission>(e =>
        {
            e.HasKey(mip => new { mip.MenuItemId, mip.PermissionId });
            e.HasOne(mip => mip.MenuItem)
             .WithMany(mi => mi.MenuItemPermissions)
             .HasForeignKey(mip => mip.MenuItemId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(mip => mip.Permission)
             .WithMany(p => p.MenuItemPermissions)
             .HasForeignKey(mip => mip.PermissionId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private sealed class NoopCurrentUserScope : ICurrentUserScope
    {
        public static NoopCurrentUserScope Instance { get; } = new();

        public bool IsActive => false;
        public int? FacultyId => null;

        public Task<bool> IsActiveAsync() => Task.FromResult(false);
        public Task<int?> GetFacultyIdAsync() => Task.FromResult<int?>(null);
    }
}
