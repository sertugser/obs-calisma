using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Application.StudentCard.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services.Gpa;

namespace SIS.Infrastructure.Services;

/// <summary>
/// Öğrencinin transkript (transcript) belgesini kuran servis.
/// Logic <c>StudentController.GetTranscript</c>'tan birebir taşındı; admin endpoint'lerce
/// yeniden kullanılabilmesi için <c>studentId</c> ile anahtarlanır.
/// </summary>
public class StudentTranscriptService : IStudentTranscriptService
{
    private readonly AppDbContext _context;
    private readonly IGradeVisibilityService _gradeVisibility;

    public StudentTranscriptService(
        AppDbContext context,
        IGradeVisibilityService? gradeVisibility = null)
    {
        _context = context;
        _gradeVisibility = gradeVisibility
            ?? new GradeVisibilityService(context, new NullWebModuleControlService());
    }

    public async Task<StudentTranscriptDto?> GetTranscriptAsync(int studentId, CancellationToken ct = default)
    {
        var student = await _context.Students
            .Include(s => s.StudentPrograms)
                .ThenInclude(sp => sp.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
            return null;

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == student.UserId, ct);

        if (user == null)
            return null;

        var activeProgram = student.StudentPrograms.FirstOrDefault(sp => sp.IsActive);
        if (activeProgram == null)
            return null;

        var registrations = await _context.CourseRegistrations
            .AsNoTracking()
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Course)
            .Include(cr => cr.CourseSection)
                .ThenInclude(cs => cs.Term)
            .Include(cr => cr.OpeningReasonSnapshot) // Faz 4 — IsExcludedFromGpa flag için
            .Where(cr => cr.StudentId == student.Id
                && cr.Grade != null
                && cr.Status != RegistrationStatus.Draft
                && cr.Status != RegistrationStatus.Dropped)
            .ToListAsync(ct);

        var transcriptGradeVisibility = await _gradeVisibility.BuildTermGradeVisibilityAsync(
            registrations.Select(r => r.CourseSection.TermId), ct);
        registrations = registrations
            .Where(registration => transcriptGradeVisibility.GetValueOrDefault(registration.CourseSection.TermId, true))
            .ToList();

        // Aktif dönemde kayıtlı olup henüz notu girilmemiş dersler (Transkript Senaryosu için).
        // Resmî transkripte (Terms) GİRMEZ; ayrı alanda döner. Notsuz oldukları için görünürlük
        // kapısına takılmazlar (gizlenecek not yok) — öğrencinin kendi güncel kayıtlarıdır.
        var activeTermId = await _context.AcademicTerms
            .AsNoTracking()
            .ActiveTerm()
            .Select(term => (int?)term.Id)
            .FirstOrDefaultAsync(ct);
        var inProgressRegistrations = activeTermId == null
            ? new List<CourseRegistration>()
            : await _context.CourseRegistrations
                .AsNoTracking()
                .Include(cr => cr.CourseSection)
                    .ThenInclude(cs => cs.Course)
                .Include(cr => cr.CourseSection)
                    .ThenInclude(cs => cs.Term)
                .Where(cr => cr.StudentId == student.Id
                    && cr.CourseSection.TermId == activeTermId
                    && cr.Grade == null
                    && cr.Status != RegistrationStatus.Draft
                    && cr.Status != RegistrationStatus.Dropped)
                .ToListAsync(ct);

        var registrationIds = registrations.Select(registration => registration.Id).ToList();
        var courseIds = registrations
            .Select(registration => registration.CourseSection.CourseId)
            .Concat(inProgressRegistrations.Select(registration => registration.CourseSection.CourseId))
            .Distinct()
            .ToList();
        var termIds = registrations
            .Select(registration => registration.CourseSection.TermId)
            .Distinct()
            .ToList();
        // Eşdeğerlikleri tek seferde (çift yönlü) yükle: hem zincir haritası hem de
        // ders-satırında gösterilen eşdeğer ders id'leri için kullanılır.
        var equivalencies = courseIds.Count == 0
            ? new List<CourseEquivalency>()
            : await _context.CourseEquivalencies
                .AsNoTracking()
                .Where(equivalency =>
                    courseIds.Contains(equivalency.CourseId)
                    || courseIds.Contains(equivalency.EquivalentCourseId))
                .ToListAsync(ct);
        var equivalentCourseIdsByCourseId = equivalencies
            .Where(equivalency => courseIds.Contains(equivalency.CourseId))
            .GroupBy(equivalency => equivalency.CourseId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(equivalency => equivalency.EquivalentCourseId).ToHashSet());
        var courseChainIdByCourseId = GpaMath.BuildCourseChainIdMap(courseIds, equivalencies);
        var registrationMetadataById = registrations.ToDictionary(
            registration => registration.Id,
            registration => (registration.CourseSection.CourseId, registration.CourseSection.TermId));
        var gradeComponentStatsByCourseTerm = courseIds.Count == 0 || termIds.Count == 0
            ? new Dictionary<(int CourseId, int TermId), (decimal TotalWeight, int ComponentCount)>()
            : (await _context.GradeComponents
                .AsNoTracking()
                .Where(component => courseIds.Contains(component.CourseId) && termIds.Contains(component.AcademicTermId))
                .Select(component => new
                {
                    component.CourseId,
                    TermId = component.AcademicTermId,
                    component.Id,
                    component.Weight
                })
                .ToListAsync(ct))
                .GroupBy(component => (component.CourseId, component.TermId))
                .ToDictionary(
                    group => group.Key,
                    group => (
                        TotalWeight: group.Sum(component => component.Weight),
                        ComponentCount: group.Select(component => component.Id).Distinct().Count()));
        var weightedAverageMap = registrationIds.Count == 0
            ? new Dictionary<int, decimal?>()
            : (await _context.StudentGrades
                .AsNoTracking()
                .Include(grade => grade.GradeComponent)
                // Bütünleme/mazeret puanı (MakeupScore) varsa Score'un yerine aynı ağırlıkla geçer; satır ikisinden biri doluysa sayılır.
                .Where(grade => registrationIds.Contains(grade.CourseRegistrationId) && (grade.Score != null || grade.MakeupScore != null))
                .ToListAsync(ct))
                .GroupBy(grade => grade.CourseRegistrationId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        if (!registrationMetadataById.TryGetValue(group.Key, out var registrationMetadata))
                            return (decimal?)null;

                        if (!gradeComponentStatsByCourseTerm.TryGetValue(
                                (registrationMetadata.CourseId, registrationMetadata.TermId),
                                out var componentStats)
                            || componentStats.TotalWeight <= 0
                            || componentStats.ComponentCount <= 0)
                            return (decimal?)null;

                        var gradedComponents = group
                            .Where(item => item.GradeComponent != null)
                            .GroupBy(item => item.GradeComponentId)
                            .Select(componentGroup =>
                            {
                                var grade = componentGroup.First();
                                return new
                                {
                                    Score = (grade.MakeupScore ?? grade.Score)!.Value,
                                    grade.GradeComponent.Weight
                                };
                            })
                            .ToList();

                        var actualWeight = gradedComponents.Sum(item => item.Weight);
                        if (gradedComponents.Count != componentStats.ComponentCount || actualWeight != componentStats.TotalWeight)
                            return (decimal?)null;

                        var totalWeightedScore = gradedComponents.Sum(item => item.Score * item.Weight / 100m);
                        return Math.Round(
                            totalWeightedScore * 100m / componentStats.TotalWeight,
                            2,
                            MidpointRounding.AwayFromZero);
                    });

        var repeatedChainCounts = registrations
            .GroupBy(registration => courseChainIdByCourseId.GetValueOrDefault(registration.CourseSection.CourseId, registration.CourseSection.CourseId))
            .ToDictionary(group => group.Key, group => group.Count());
        var latestGradesByCourse = new Dictionary<int, CourseRegistration>();
        var termGroups = registrations
            .GroupBy(cr => cr.CourseSection.TermId)
            .Select(group => new
            {
                Term = group.First().CourseSection.Term,
                Registrations = group
                    .OrderBy(cr => cr.CourseSection.Course.Code)
                    .ThenBy(cr => cr.Id)
                    .ToList()
            })
            .OrderBy(group => group.Term.TermStart)
            .ThenBy(group => group.Term.Id)
            .ToList();

        var transcriptTerms = new List<StudentTranscriptTermDto>(termGroups.Count);

        foreach (var termGroup in termGroups)
        {
            foreach (var registration in termGroup.Registrations
                .OrderBy(registration => registration.Id))
            {
                var chainId = courseChainIdByCourseId.GetValueOrDefault(
                    registration.CourseSection.CourseId,
                    registration.CourseSection.CourseId);
                latestGradesByCourse[chainId] = registration;
            }

            var courseDtos = termGroup.Registrations
                .Select(registration =>
                {
                    var course = registration.CourseSection.Course;
                    var chainId = courseChainIdByCourseId.GetValueOrDefault(course.Id, course.Id);
                    var grade = registration.Grade!.Value;
                    var gradePoint = GradePointMap.GetPoint(grade);
                    var countsTowardsGpa = GpaMath.CountsTowardsGpa(registration);
                    var weightedEcts = countsTowardsGpa
                        ? (decimal?)GpaMath.GetWeightedEcts(registration)
                        : null;

                    return new StudentTranscriptCourseDto
                    {
                        RegistrationId = registration.Id,
                        CourseId = course.Id,
                        CourseCode = course.Code,
                        CourseName = course.Name,
                        Credits = GpaMath.GetCredit(registration),
                        Ects = GpaMath.GetEcts(registration),
                        EquivalentCourseIds = equivalentCourseIdsByCourseId.TryGetValue(course.Id, out var equivalentIds)
                            ? equivalentIds.OrderBy(id => id).ToList()
                            : new List<int>(),
                        AverageScore = weightedAverageMap.GetValueOrDefault(registration.Id),
                        Grade = grade.ToString(),
                        Status = registration.Status.ToString(),
                        IsRepeated = registration.IsRepeated || repeatedChainCounts.GetValueOrDefault(chainId) > 1,
                        IsPassed = GpaMath.IsPassedGrade(grade),
                        CountsTowardsGpa = countsTowardsGpa,
                        GradePoint = gradePoint,
                        WeightedEcts = weightedEcts
                    };
                })
                .ToList();

            var attemptedCredits = courseDtos.Sum(course => course.Credits);
            var attemptedEcts = courseDtos.Sum(course => course.Ects);
            var earnedCredits = courseDtos.Where(course => course.IsPassed).Sum(course => course.Credits);
            var earnedEcts = courseDtos.Where(course => course.IsPassed).Sum(course => course.Ects);
            var gpaEcts = courseDtos.Where(course => course.CountsTowardsGpa).Sum(course => course.Ects);
            var gpaPoints = courseDtos.Sum(course => course.WeightedEcts ?? 0m);

            var cumulativeRegistrations = latestGradesByCourse.Values.ToList();
            var cumulativeGpaEcts = cumulativeRegistrations.Sum(GpaMath.GetGpaEcts);
            var cumulativeGpaPoints = cumulativeRegistrations.Sum(GpaMath.GetWeightedEcts);

            transcriptTerms.Add(new StudentTranscriptTermDto
            {
                TermId = termGroup.Term.Id,
                TermName = termGroup.Term.Name,
                Year = termGroup.Term.Year,
                Semester = termGroup.Term.TermType.ToString(),
                AttemptedCredits = attemptedCredits,
                AttemptedEcts = attemptedEcts,
                EarnedCredits = earnedCredits,
                EarnedEcts = earnedEcts,
                GpaEcts = gpaEcts,
                GpaPoints = Math.Round(gpaPoints, 2, MidpointRounding.AwayFromZero),
                CumulativeGpaEcts = cumulativeGpaEcts,
                CumulativeGpaPoints = Math.Round(cumulativeGpaPoints, 2, MidpointRounding.AwayFromZero),
                Sgpa = GpaMath.CalculateAverage(termGroup.Registrations),
                Cgpa = GpaMath.CalculateAverage(cumulativeRegistrations),
                Courses = courseDtos
            });
        }

        var latestRegistrations = latestGradesByCourse.Values.ToList();
        var totalGpaEcts = latestRegistrations.Sum(GpaMath.GetGpaEcts);
        var totalGpaPoints = latestRegistrations.Sum(GpaMath.GetWeightedEcts);

        var inProgressCourses = inProgressRegistrations
            .OrderBy(registration => registration.CourseSection.Course.Code)
            .ThenBy(registration => registration.Id)
            .Select(registration =>
            {
                var course = registration.CourseSection.Course;
                var term = registration.CourseSection.Term;
                return new StudentTranscriptInProgressCourseDto
                {
                    RegistrationId = registration.Id,
                    CourseId = course.Id,
                    CourseCode = course.Code,
                    CourseName = course.Name,
                    Credits = GpaMath.GetCredit(registration),
                    Ects = GpaMath.GetEcts(registration),
                    EquivalentCourseIds = equivalentCourseIdsByCourseId.TryGetValue(course.Id, out var equivalentIds)
                        ? equivalentIds.OrderBy(id => id).ToList()
                        : new List<int>(),
                    TermId = term.Id,
                    TermName = term.Name,
                    Year = term.Year,
                    Semester = term.TermType.ToString(),
                };
            })
            .ToList();

        var transcript = new StudentTranscriptDto
        {
            StudentId = student.Id,
            StudentNumber = student.StudentNumber,
            FullName = $"{user.FirstName} {user.LastName}",
            FacultyName = activeProgram.Program?.Department?.Faculty?.Name ?? string.Empty,
            DepartmentName = activeProgram.Program?.Department?.Name ?? string.Empty,
            ProgramName = activeProgram.Program?.Name ?? string.Empty,
            StudentStatus = student.Status.ToString(),
            EnrollmentDate = activeProgram.EnrollmentDate,
            GeneratedAt = DateTime.UtcNow,
            CurrentCgpa = transcriptTerms.Count > 0 ? transcriptTerms[^1].Cgpa : student.Cgpa,
            TotalAttemptedCredits = latestRegistrations.Sum(GpaMath.GetCredit),
            TotalAttemptedEcts = latestRegistrations.Sum(GpaMath.GetEcts),
            TotalEarnedCredits = latestRegistrations.Where(reg => GpaMath.IsPassedGrade(reg.Grade!.Value)).Sum(GpaMath.GetCredit),
            TotalEarnedEcts = latestRegistrations.Where(reg => GpaMath.IsPassedGrade(reg.Grade!.Value)).Sum(GpaMath.GetEcts),
            TotalGpaEcts = totalGpaEcts,
            TotalGpaPoints = Math.Round(totalGpaPoints, 2, MidpointRounding.AwayFromZero),
            Terms = transcriptTerms,
            InProgressCourses = inProgressCourses
        };

        return transcript;
    }
}
