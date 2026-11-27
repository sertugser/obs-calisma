using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Tests.TestDoubles;

namespace SIS.Tests.Application;

public class AdvisorConstraintRegistrationTests : IDisposable
{
    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int ActiveTermId = 1;
    private const int AdvisorId = 10;
    private const int AdvisorUserId = 100;
    private const int StudentId = 1;
    private const int StudentUserId = 200;

    private readonly AppDbContext _context;
    private readonly CourseRegistrationService _service;

    public AdvisorConstraintRegistrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        SeedBaseData();

        var clock = new FakeClock();
        var validationService = new RegistrationValidationService(_context, clock);
        var constraintService = new AdvisorConstraintService(_context);
        _service = new CourseRegistrationService(_context, validationService, clock, new SabitKurumSaatDilimi("Etc/UTC"), advisorConstraintService: constraintService);
    }

    [Fact]
    public async Task SubmitRegistration_BlockConstraint_PreventsRegistration()
    {
        var course = AddCourse(100, "CENG201", "Data Structures");
        var section = AddSection(1000, course.Id);
        AddConstraint(course.Id, AdvisorConstraintType.Block, "Bu donem alinmamali.");

        var result = await _service.SubmitRegistrationAsync(StudentId, new RegistrationSubmitDto
        {
            CourseSectionIds = [section.Id]
        });

        result.Success.Should().BeFalse();
        result.ValidationResult.Should().NotBeNull();
        result.ValidationResult!.Errors.Should().ContainSingle(error =>
            error.Rule == "AdvisorConstraintBlock"
            && error.Message.Contains("CENG201"));
    }

    [Fact]
    public async Task SubmitRegistration_WarningConstraint_AllowsRegistrationAndReturnsWarning()
    {
        var course = AddCourse(101, "CENG202", "Algorithms");
        var section = AddSection(1001, course.Id);
        AddConstraint(course.Id, AdvisorConstraintType.Warning, "Yuku agir olabilir.");

        var result = await _service.SubmitRegistrationAsync(StudentId, new RegistrationSubmitDto
        {
            CourseSectionIds = [section.Id]
        });

        result.Success.Should().BeTrue();
        result.ValidationResult.Should().NotBeNull();
        result.ValidationResult!.Warnings.Should().ContainSingle(warning =>
            warning.Rule == "AdvisorConstraintWarning"
            && warning.Message.Contains("CENG202"));
        result.Registrations.Should().ContainSingle(r => r.Status == RegistrationStatus.PendingAdvisorApproval.ToString());
    }

    [Fact]
    public async Task SubmitRegistration_WithDraftPlanItem_DoesNotReportSelfDuplicate()
    {
        var course = AddCourse(102, "CENG203", "Databases");
        var section = AddSection(1002, course.Id);

        var addResult = await _service.AddToPlanAsync(StudentId, section.Id);
        addResult.Success.Should().BeTrue();

        var result = await _service.SubmitRegistrationAsync(StudentId, new RegistrationSubmitDto
        {
            CourseSectionIds = [section.Id]
        });

        result.Success.Should().BeTrue();
        result.ValidationResult!.Errors.Should().NotContain(error => error.Rule == "DuplicateRegistration");
        _context.CourseRegistrations.Single(registration => registration.CourseSectionId == section.Id)
            .Status.Should().Be(RegistrationStatus.PendingAdvisorApproval);
    }

    [Fact]
    public async Task AdvisorAdd_ThenApprove_ActivatesRegistration()
    {
        var course = AddCourse(110, "CENG210", "Operating Systems");
        var section = AddSection(1010, course.Id);

        // Danışman öğrenci adına ekler → PendingAdvisorApproval (Proliz modeli)
        var addResult = await _service.AddToPlanAsync(StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);
        addResult.Success.Should().BeTrue();
        _context.CourseRegistrations.Single(r => r.CourseSectionId == section.Id)
            .Status.Should().Be(RegistrationStatus.PendingAdvisorApproval);

        // Danışman ekleyince plan "onay bekler" (Submitted) duruma gelir
        _context.RegistrationPlans.Single(p => p.StudentId == StudentId && p.TermId == ActiveTermId)
            .Status.Should().Be(RegistrationPlanStatus.Submitted);

        // Danışman onaylar → eklenen ders Active olur (eski bug: Draft'ta kalıp yok sayılıyordu)
        var approveResult = await _service.ApproveStudentRegistrationsAsync(
            AdvisorId, new ApprovalActionDto { StudentId = StudentId });
        approveResult.Success.Should().BeTrue();
        approveResult.AffectedRegistrations.Should().Be(1);
        _context.CourseRegistrations.Single(r => r.CourseSectionId == section.Id)
            .Status.Should().Be(RegistrationStatus.Active);
    }

    [Fact]
    public async Task GetStudentRegistrations_ReturnsProlizColumns()
    {
        var course = AddCourse(111, "CENG211", "Networks");
        var section = AddSection(1011, course.Id);
        await _service.AddToPlanAsync(StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);

        var rows = await _service.GetStudentRegistrationsAsync(StudentId);

        var row = rows.Single(r => r.CourseSectionId == section.Id);
        row.SectionNumber.Should().Be(1);
        row.NationalCredit.Should().Be(3);
        row.IsCompulsory.Should().BeTrue();
        row.SuggestedSemester.Should().Be(4);
        row.TakeType.Should().Be("Zorunlu");
    }

    [Fact]
    public async Task AdvisorApprove_WhenPrerequisiteMissing_BlocksWithValidationError()
    {
        var baseCourse = AddCourse(120, "CENG120", "Intro to Programming");
        var advanced = AddCourse(121, "CENG320", "Advanced Topics");
        _context.CoursePrerequisites.Add(new CoursePrerequisite
        {
            CourseId = advanced.Id,
            RequiredCourseId = baseCourse.Id
        });
        var section = AddSection(1020, advanced.Id);

        // Doğrudan PendingAdvisorApproval kayıt (AddToPlan önkoşul eksikliğinde zaten bloklardı).
        _context.RegistrationPlans.Add(new RegistrationPlan
        {
            StudentId = StudentId,
            TermId = ActiveTermId,
            Status = RegistrationPlanStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        _context.CourseRegistrations.Add(new SIS.Domain.Entities.CourseRegistration
        {
            StudentId = StudentId,
            CourseSectionId = section.Id,
            Status = RegistrationStatus.PendingAdvisorApproval,
            CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        // "Kontrol Et ve Onayla": önkoşul eksik → onaylanmaz, hata döner
        var result = await _service.ApproveStudentRegistrationsAsync(
            AdvisorId, new ApprovalActionDto { StudentId = StudentId });

        result.Success.Should().BeFalse();
        result.ValidationResult.Should().NotBeNull();
        result.ValidationResult!.Errors.Should().Contain(e => e.Rule == "Prerequisite");
        // Onaylanmadığı için hâlâ PendingAdvisorApproval kalmalı
        _context.CourseRegistrations.Single(r => r.CourseSectionId == section.Id)
            .Status.Should().Be(RegistrationStatus.PendingAdvisorApproval);
    }

    [Fact]
    public async Task AdvisorDrop_DuringRegistrationWindow_Succeeds()
    {
        // Dönem kayıt penceresinde (add-drop henüz açılmadı). Danışman Pending kaydı çıkarabilmeli.
        var course = AddCourse(130, "CENG130", "Embedded Systems");
        var section = AddSection(1030, course.Id);
        var add = await _service.AddToPlanAsync(StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);
        add.Success.Should().BeTrue();
        var reg = _context.CourseRegistrations.Single(r => r.CourseSectionId == section.Id);

        var drop = await _service.DropCourseAsync(StudentId, reg.Id, asAdvisor: true);

        drop.Success.Should().BeTrue();
        _context.CourseRegistrations.Single(r => r.Id == reg.Id).Status.Should().Be(RegistrationStatus.Dropped);
    }

    [Fact]
    public async Task StudentDrop_OfFinalizedRegistration_IsBlocked()
    {
        var course = AddCourse(131, "CENG131", "Compilers");
        var section = AddSection(1031, course.Id);
        await _service.AddToPlanAsync(StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);
        var reg = _context.CourseRegistrations.Single(r => r.CourseSectionId == section.Id);

        // Öğrenci kesinleştirilmiş (Pending) kaydı çıkaramaz — yalnızca danışman.
        var drop = await _service.DropCourseAsync(StudentId, reg.Id, asAdvisor: false);

        drop.Success.Should().BeFalse();
        drop.ValidationResult!.Errors.Should().Contain(e => e.Rule == "FinalizedLocked");
        _context.CourseRegistrations.Single(r => r.Id == reg.Id).Status.Should().Be(RegistrationStatus.PendingAdvisorApproval);
    }

    [Fact]
    public async Task EnsureFailedCourses_AutoAddsFailedCourseToPlan()
    {
        // Geçmiş dönemde FF alınmış ders + aktif dönemde açık şube.
        var pastTerm = new AcademicTerm
        {
            Id = 2,
            Name = "2024-2025 Güz",
            Year = 2024,
            TermType = TermType.Fall,
            IsActivated = false,
            TermStart = DateTime.UtcNow.AddYears(-1),
            TermEnd = DateTime.UtcNow.AddYears(-1).AddDays(100)
        };
        _context.AcademicTerms.Add(pastTerm);

        var course = AddCourse(140, "CENG140", "Logic Design");
        _context.CourseSections.Add(new CourseSection
        {
            Id = 1400, CourseId = course.Id, TermId = 2, SectionNumber = 1,
            Capacity = 40, EnrolledCount = 0, Status = CourseSectionStatus.Active
        });
        _context.CourseRegistrations.Add(new SIS.Domain.Entities.CourseRegistration
        {
            StudentId = StudentId, CourseSectionId = 1400,
            Status = RegistrationStatus.Completed, Grade = GradeCode.FF,
            CreatedAt = DateTime.UtcNow.AddYears(-1)
        });
        var activeSection = AddSection(1401, course.Id);
        _context.SaveChanges();

        var result = await _service.EnsureFailedCoursesInPlanAsync(StudentId);

        result.Success.Should().BeTrue();
        _context.CourseRegistrations
            .Any(r => r.CourseSectionId == activeSection.Id
                && r.StudentId == StudentId
                && r.Status == RegistrationStatus.Draft)
            .Should().BeTrue();
    }

    private void SeedBaseData()
    {
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Engineering", Code = "ENG" });
        _context.Departments.Add(new Department
        {
            Id = DepartmentId,
            Name = "Computer Engineering",
            Code = "CENG",
            FacultyId = FacultyId
        });
        var termEnd = DateTime.UtcNow.AddDays(90);
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = ActiveTermId,
            Name = "2025-2026 Fall",
            Year = 2025,
            TermType = TermType.Fall,
            IsActivated = true,
            TermStart = DateTime.UtcNow.AddDays(-10),
            TermEnd = termEnd,
            AcademicCalendar = new SIS.Domain.Entities.TermAcademicCalendar
            {
                RegistrationStart = DateTime.UtcNow.AddDays(-2),
                RegistrationEnd = DateTime.UtcNow.AddDays(5),
                AddDropStart = DateTime.UtcNow.AddDays(6),
                AddDropEnd = DateTime.UtcNow.AddDays(12),
                WithdrawalDeadline = DateTime.UtcNow.AddDays(50),
                GradeEntryStart = termEnd.AddDays(-14),
                GradeEntryEnd = termEnd.AddDays(14),
                CreatedAt = DateTime.UtcNow,
            }
        });
        // Yayın kapısı (draft/publish): fakülte programı onaylı olmalı, yoksa AddToPlan/Submit bloke olur.
        _context.FacultyScheduleApprovals.Add(new FacultyScheduleApproval
        {
            Id = 1,
            FacultyId = FacultyId,
            TermId = ActiveTermId,
            ApprovedByUserId = AdvisorUserId,
            ApprovedAt = DateTime.UtcNow
        });
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            Name = "Computer Engineering",
            Code = "CENG-BSC",
            DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });
        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Version = "2024",
            IsActive = true
        });
        _context.Users.Add(new ApplicationUser
        {
            Id = AdvisorUserId,
            UserName = "advisor",
            NormalizedUserName = "ADVISOR",
            Email = "advisor@example.com",
            FirstName = "Ayse",
            LastName = "Danisman"
        });
        _context.Instructors.Add(new Instructor
        {
            Id = AdvisorId,
            UserId = AdvisorUserId,
            DepartmentId = DepartmentId,
            Title = "Dr.",
            MaxWeeklyHours = 20
        });
        _context.Users.Add(new ApplicationUser
        {
            Id = StudentUserId,
            UserName = "student",
            NormalizedUserName = "STUDENT",
            Email = "student@example.com",
            FirstName = "Ali",
            LastName = "Ogrenci"
        });
        _context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = StudentUserId,
            StudentNumber = "20240001",
            NationalId = "12345678901",
            Status = StudentStatus.Active,
            Cgpa = 2.50m,
            Sgpa = 2.60m,
            CompletedSemesters = 3,
            IsScholarshipStudent = false,
            HasFinancialBlock = false,
            AdvisorId = AdvisorId
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow.AddYears(-2),
            IsActive = true
        });

        _context.SaveChanges();
    }

    private Course AddCourse(int id, string code, string name)
    {
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = name,
            DepartmentId = DepartmentId,
            Ects = 6,
            NationalCredit = 3,
            TheoryHours = 3,
            LabHours = 0
        };
        _context.Courses.Add(course);
        _context.CurriculumCourses.Add(new CurriculumCourse
        {
            CurriculumId = CurriculumId,
            CourseId = id,
            IsCompulsory = true,
            SuggestedSemester = 4
        });
        _context.SaveChanges();
        return course;
    }

    private CourseSection AddSection(int id, int courseId)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            TermId = ActiveTermId,
            SectionNumber = 1,
            Capacity = 40,
            EnrolledCount = 0,
            Status = CourseSectionStatus.Active
        };
        _context.CourseSections.Add(section);
        _context.SaveChanges();
        return section;
    }

    private void AddConstraint(int courseId, AdvisorConstraintType type, string note)
    {
        _context.AdvisorCourseConstraints.Add(new AdvisorCourseConstraint
        {
            StudentId = StudentId,
            CourseId = courseId,
            TermId = ActiveTermId,
            AdvisorId = AdvisorId,
            ConstraintType = type,
            Note = note,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
