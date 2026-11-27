using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Admin.DTOs;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Application.Notifications.DTOs;
using SIS.Application.Notifications.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Tests.TestDoubles;

namespace SIS.Tests.Application;

public class AdvisorApprovalServiceTests : IDisposable
{
    // Tetiklenen bildirimleri yakalayan minimal sahte servis.
    private sealed class RecordingNotifications : INotificationService
    {
        public List<(int UserId, NotificationType Type)> Sent { get; } = new();

        public Task NotifyAsync(int userId, NotificationType type, string title, string body,
            string? link = null, NotificationSeverity severity = NotificationSeverity.Info,
            string? entityRef = null, string? groupKey = null, DateTime? expiresAt = null, CancellationToken ct = default)
        {
            Sent.Add((userId, type));
            return Task.CompletedTask;
        }

        public Task NotifyManyAsync(IEnumerable<int> userIds, NotificationType type, string title, string body,
            string? link = null, NotificationSeverity severity = NotificationSeverity.Info, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<PagedResult<NotificationDto>> GetForUserAsync(int userId, NotificationFilter filter, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<NotificationDto>(new List<NotificationDto>(), 0, 1, 20));

        public Task<IReadOnlyList<NotificationDto>> GetRecentAsync(int userId, int max, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NotificationDto>>(new List<NotificationDto>());

        public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default) => Task.FromResult(0);
        public Task MarkReadAsync(int userId, int id, CancellationToken ct = default) => Task.CompletedTask;
        public Task MarkAllReadAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int userId, int id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private readonly AppDbContext _context;
    private readonly CourseRegistrationService _service;
    private readonly RecordingNotifications _notifications = new();

    private const int FacultyId = 1;
    private const int DepartmentId = 1;
    private const int ProgramId = 1;
    private const int CurriculumId = 1;
    private const int TermId = 1;
    private const int AdvisorInstructorId = 10;
    private const int AdvisorUserId = 100;
    private const int StudentId = 1;
    private const int StudentUserId = 200;
    private const int OtherAdvisorInstructorId = 20;
    private const int OtherAdvisorUserId = 101;

    public AdvisorApprovalServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        var clock = new FakeClock();
        var validationService = new RegistrationValidationService(_context, clock);
        _service = new CourseRegistrationService(_context, validationService, clock, new SabitKurumSaatDilimi("Etc/UTC"), notifications: _notifications);

        SeedBaseData();
    }

    private void SeedBaseData()
    {
        // Faculty
        _context.Faculties.Add(new Faculty { Id = FacultyId, Name = "Engineering", Code = "ENG" });

        // Department
        _context.Departments.Add(new Department
        {
            Id = DepartmentId,
            Name = "Computer Engineering",
            Code = "CE",
            FacultyId = FacultyId
        });

        // Active academic term
        var termEnd = DateTime.UtcNow.AddDays(90);
        _context.AcademicTerms.Add(new AcademicTerm
        {
            Id = TermId,
            Name = "2025-2026 Spring",
            Year = 2025,
            TermType = TermType.Spring,
            IsActivated = true,
            TermStart = DateTime.UtcNow.AddDays(-10),
            TermEnd = termEnd,
            AcademicCalendar = new SIS.Domain.Entities.TermAcademicCalendar
            {
                RegistrationStart = DateTime.UtcNow.AddDays(-5),
                RegistrationEnd = DateTime.UtcNow.AddDays(5),
                AddDropStart = DateTime.UtcNow.AddDays(6),
                AddDropEnd = DateTime.UtcNow.AddDays(12),
                WithdrawalDeadline = DateTime.UtcNow.AddDays(49),
                GradeEntryStart = termEnd.AddDays(-14),
                GradeEntryEnd = termEnd.AddDays(14),
                CreatedAt = DateTime.UtcNow,
            }
        });

        // Academic program
        _context.AcademicPrograms.Add(new AcademicProgram
        {
            Id = ProgramId,
            Name = "Computer Engineering",
            Code = "CE-BSC",
            DepartmentId = DepartmentId,
            DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester,
            MaxStudyDurationSemesters = 14,
            TotalRequiredEcts = 240
        });

        // Curriculum
        _context.Curricula.Add(new Curriculum
        {
            Id = CurriculumId,
            ProgramId = ProgramId,
            Version = "2022",
            IsActive = true
        });

        // Advisor user and instructor
        _context.Users.Add(new ApplicationUser
        {
            Id = AdvisorUserId,
            UserName = "advisor1",
            NormalizedUserName = "ADVISOR1",
            Email = "advisor1@example.com",
            FirstName = "Ali",
            LastName = "Yilmaz"
        });
        _context.Instructors.Add(new Instructor
        {
            Id = AdvisorInstructorId,
            UserId = AdvisorUserId,
            DepartmentId = DepartmentId,
            Title = "Dr.",
            MaxWeeklyHours = 20
        });

        // Student user and student
        _context.Users.Add(new ApplicationUser
        {
            Id = StudentUserId,
            UserName = "student1",
            NormalizedUserName = "STUDENT1",
            Email = "student1@example.com",
            FirstName = "Ahmet",
            LastName = "Demir"
        });
        _context.Students.Add(new Student
        {
            Id = StudentId,
            UserId = StudentUserId,
            StudentNumber = "20220001",
            NationalId = "12345678901",
            Status = StudentStatus.Active,
            Cgpa = 2.80m,
            Sgpa = 2.80m,
            CompletedSemesters = 4,
            FrozenSemesterCount = 0,
            WithdrawalCount = 0,
            IsScholarshipStudent = false,
            HasFinancialBlock = false,
            AdvisorId = AdvisorInstructorId
        });

        // StudentProgram
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = StudentId,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow.AddYears(-2),
            IsActive = true
        });

        // Faculty schedule approval: gate passes for courses belonging to this faculty.
        _context.FacultyScheduleApprovals.Add(new FacultyScheduleApproval
        {
            FacultyId = FacultyId,
            TermId = TermId,
            ApprovedByUserId = AdvisorUserId,
            ApprovedAt = DateTime.UtcNow
        });

        _context.SaveChanges();
    }

    // ──────────────────────────── Helpers ────────────────────────────

    private Course AddCourse(int id, string code, string name, int ects)
    {
        var course = new Course
        {
            Id = id,
            Code = code,
            Name = name,
            Ects = ects,
            DepartmentId = DepartmentId,
            TheoryHours = 3,
            LabHours = 0,
            IsNonGpa = false
        };
        _context.Courses.Add(course);
        _context.SaveChanges();
        return course;
    }

    private CourseSection AddSection(int id, int courseId, int capacity, int enrolledCount = 0)
    {
        var section = new CourseSection
        {
            Id = id,
            CourseId = courseId,
            TermId = TermId,
            Capacity = capacity,
            EnrolledCount = enrolledCount,
            Status = CourseSectionStatus.Active,
            InstructorId = AdvisorInstructorId
        };
        _context.CourseSections.Add(section);
        _context.SaveChanges();
        return section;
    }

    private CourseRegistration AddRegistration(
        int id, int studentId, int sectionId, RegistrationStatus status)
    {
        var reg = new CourseRegistration
        {
            Id = id,
            StudentId = studentId,
            CourseSectionId = sectionId,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };
        _context.CourseRegistrations.Add(reg);
        _context.SaveChanges();
        return reg;
    }

    // ──────────────────────────── Tests ────────────────────────────

    [Fact]
    public async Task GetPendingApprovals_ReturnsPendingStudents()
    {
        var course = AddCourse(1, "CENG101", "Intro to CS", 6);
        var section = AddSection(1, course.Id, 40);
        AddRegistration(1, StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);

        var result = await _service.GetPendingApprovalsAsync(AdvisorInstructorId);

        result.Should().HaveCount(1);
        var pending = result[0];
        pending.StudentId.Should().Be(StudentId);
        pending.StudentNumber.Should().Be("20220001");
        pending.StudentName.Should().Contain("Ahmet");
        pending.Cgpa.Should().Be(2.80m);
        pending.CompletedSemesters.Should().Be(4);
        pending.IsScholarshipStudent.Should().BeFalse();
        pending.Registrations.Should().HaveCount(1);
        pending.Registrations[0].CourseCode.Should().Be("CENG101");
        pending.Registrations[0].Ects.Should().Be(6);
        pending.TotalEcts.Should().Be(6);
    }

    [Fact]
    public async Task GetPendingApprovals_ExcludesOtherAdvisorsStudents()
    {
        // Add a second student with a different advisor
        _context.Users.Add(new ApplicationUser
        {
            Id = 300,
            UserName = "student2",
            NormalizedUserName = "STUDENT2",
            Email = "student2@example.com",
            FirstName = "Mehmet",
            LastName = "Kaya"
        });
        _context.Users.Add(new ApplicationUser
        {
            Id = OtherAdvisorUserId,
            UserName = "advisor2",
            NormalizedUserName = "ADVISOR2",
            Email = "advisor2@example.com",
            FirstName = "Zeynep",
            LastName = "Arslan"
        });
        _context.Instructors.Add(new Instructor
        {
            Id = OtherAdvisorInstructorId,
            UserId = OtherAdvisorUserId,
            DepartmentId = DepartmentId,
            Title = "Prof.",
            MaxWeeklyHours = 20
        });
        _context.Students.Add(new Student
        {
            Id = 2,
            UserId = 300,
            StudentNumber = "20220002",
            NationalId = "98765432101",
            Status = StudentStatus.Active,
            Cgpa = 3.00m,
            Sgpa = 3.00m,
            CompletedSemesters = 3,
            FrozenSemesterCount = 0,
            WithdrawalCount = 0,
            IsScholarshipStudent = false,
            HasFinancialBlock = false,
            AdvisorId = OtherAdvisorInstructorId  // different advisor
        });
        _context.StudentPrograms.Add(new StudentProgram
        {
            StudentId = 2,
            ProgramId = ProgramId,
            CurriculumId = CurriculumId,
            Type = StudentProgramType.Main,
            EnrollmentDate = DateTime.UtcNow.AddYears(-1),
            IsActive = true
        });
        _context.SaveChanges();

        var course = AddCourse(1, "CENG101", "Intro to CS", 6);
        var section = AddSection(1, course.Id, 40);
        AddRegistration(1, 2, section.Id, RegistrationStatus.PendingAdvisorApproval);

        var result = await _service.GetPendingApprovalsAsync(AdvisorInstructorId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingApprovals_ExcludesActiveRegistrations()
    {
        var course = AddCourse(1, "CENG101", "Intro to CS", 6);
        var section = AddSection(1, course.Id, 40);
        AddRegistration(1, StudentId, section.Id, RegistrationStatus.Active);

        var result = await _service.GetPendingApprovalsAsync(AdvisorInstructorId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Approve_ChangesStatusToActive()
    {
        var course = AddCourse(1, "CENG101", "Intro to CS", 6);
        var section = AddSection(1, course.Id, 40);
        AddRegistration(1, StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);

        var dto = new ApprovalActionDto { StudentId = StudentId };
        var result = await _service.ApproveStudentRegistrationsAsync(AdvisorInstructorId, dto);

        result.Success.Should().BeTrue();
        result.AffectedRegistrations.Should().Be(1);

        var reg = await _context.CourseRegistrations.FindAsync(1);
        reg!.Status.Should().Be(RegistrationStatus.Active);
    }

    [Fact]
    public async Task Approve_FailsForWrongAdvisor()
    {
        var course = AddCourse(1, "CENG101", "Intro to CS", 6);
        var section = AddSection(1, course.Id, 40);
        AddRegistration(1, StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);

        var dto = new ApprovalActionDto { StudentId = StudentId };
        var result = await _service.ApproveStudentRegistrationsAsync(OtherAdvisorInstructorId, dto);

        result.Success.Should().BeFalse();

        var reg = await _context.CourseRegistrations.FindAsync(1);
        reg!.Status.Should().Be(RegistrationStatus.PendingAdvisorApproval);
    }

    [Fact]
    public async Task Approve_IgnoresNonPendingRegistrations()
    {
        var course1 = AddCourse(1, "CENG101", "Intro to CS", 6);
        var course2 = AddCourse(2, "CENG102", "Data Structures", 6);
        var section1 = AddSection(1, course1.Id, 40);
        var section2 = AddSection(2, course2.Id, 40);

        AddRegistration(1, StudentId, section1.Id, RegistrationStatus.PendingAdvisorApproval);
        AddRegistration(2, StudentId, section2.Id, RegistrationStatus.Active);

        var dto = new ApprovalActionDto { StudentId = StudentId };
        var result = await _service.ApproveStudentRegistrationsAsync(AdvisorInstructorId, dto);

        result.Success.Should().BeTrue();
        result.AffectedRegistrations.Should().Be(1);

        var pendingReg = await _context.CourseRegistrations.FindAsync(1);
        pendingReg!.Status.Should().Be(RegistrationStatus.Active);

        var activeReg = await _context.CourseRegistrations.FindAsync(2);
        activeReg!.Status.Should().Be(RegistrationStatus.Active);
    }

    [Fact]
    public async Task Reject_ChangesStatusToDroppedAndDecrementsEnrolledCount()
    {
        var course = AddCourse(1, "CENG101", "Intro to CS", 6);
        var section = AddSection(1, course.Id, 40, enrolledCount: 1);
        AddRegistration(1, StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);

        var dto = new ApprovalActionDto { StudentId = StudentId };
        var result = await _service.RejectStudentRegistrationsAsync(AdvisorInstructorId, dto);

        result.Success.Should().BeTrue();
        result.AffectedRegistrations.Should().Be(1);

        var reg = await _context.CourseRegistrations.FindAsync(1);
        reg!.Status.Should().Be(RegistrationStatus.Rejected);

        var updatedSection = await _context.CourseSections.FindAsync(1);
        updatedSection!.EnrolledCount.Should().Be(0);
    }

    [Fact]
    public async Task Reject_FailsForWrongAdvisor()
    {
        var course = AddCourse(1, "CENG101", "Intro to CS", 6);
        var section = AddSection(1, course.Id, 40, enrolledCount: 1);
        AddRegistration(1, StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);

        var dto = new ApprovalActionDto { StudentId = StudentId };
        var result = await _service.RejectStudentRegistrationsAsync(OtherAdvisorInstructorId, dto);

        result.Success.Should().BeFalse();

        var reg = await _context.CourseRegistrations.FindAsync(1);
        reg!.Status.Should().Be(RegistrationStatus.PendingAdvisorApproval);

        var updatedSection = await _context.CourseSections.FindAsync(1);
        updatedSection!.EnrolledCount.Should().Be(1);
    }

    [Fact]
    public async Task Approve_NotifiesStudent()
    {
        var course = AddCourse(1, "CENG101", "Intro to CS", 6);
        var section = AddSection(1, course.Id, 40);
        AddRegistration(1, StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);

        await _service.ApproveStudentRegistrationsAsync(AdvisorInstructorId, new ApprovalActionDto { StudentId = StudentId });

        _notifications.Sent.Should().ContainSingle(n => n.UserId == StudentUserId && n.Type == NotificationType.RegistrationApproved);
    }

    [Fact]
    public async Task Reject_NotifiesStudent()
    {
        var course = AddCourse(1, "CENG101", "Intro to CS", 6);
        var section = AddSection(1, course.Id, 40, enrolledCount: 1);
        AddRegistration(1, StudentId, section.Id, RegistrationStatus.PendingAdvisorApproval);

        await _service.RejectStudentRegistrationsAsync(AdvisorInstructorId, new ApprovalActionDto { StudentId = StudentId });

        _notifications.Sent.Should().ContainSingle(n => n.UserId == StudentUserId && n.Type == NotificationType.RegistrationRejected);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
