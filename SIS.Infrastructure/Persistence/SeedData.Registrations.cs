using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static async Task SeedRegistrationsAsync(AppDbContext context, DateTime bugun)
    {
        if (context.CourseRegistrations.Any()) return;

        var students = context.Students
            .Include(s => s.StudentPrograms)
            .ToList();
        var allCurrCourses = context.Set<CurriculumCourse>()
            .Include(cc => cc.Course)
            .ToList();
        var allPrereqs = context.CoursePrerequisites.ToList();
        var terms = context.AcademicTerms
            .Include(t => t.AcademicCalendar)
            .OrderBy(t => t.Year).ThenBy(t => t.TermType)
            .ToList();
        var instructors = context.Instructors.ToList();
        var curricula = context.Curricula.Include(c => c.Program).ToList();

        var now = bugun;
        var pastTerms = terms.Where(t => !t.IsCancelled && !(t.TermStart <= now && t.TermEnd >= now))
            .OrderBy(t => t.Year).ThenBy(t => t.TermType).ToList();

        var rng = new Random(42);
        var sectionCache = new Dictionary<(int courseId, int termId), List<CourseSection>>();
        var instructorsByDept = instructors.GroupBy(i => i.DepartmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var student in students)
        {
            var sp = student.StudentPrograms.FirstOrDefault();
            if (sp == null) continue;

            var curriculum = curricula.First(c => c.Id == sp.CurriculumId);
            var myCourses = allCurrCourses
                .Where(cc => cc.CurriculumId == curriculum.Id && cc.IsCompulsory && cc.SuggestedSemester > 0)
                .OrderBy(cc => cc.SuggestedSemester)
                .ToList();

            var deptId = curriculum.Program?.DepartmentId ?? instructors.First().DepartmentId;
            var deptInstructors = instructorsByDept.GetValueOrDefault(deptId, instructors.Take(1).ToList());
            int instrIdx = 0;

            // Latent ability recovered from the student's provisional CGPA (set in
            // SeedUsersAsync). Each course grade is drawn stochastically from this
            // ability — see SeedData.GradeDistribution.
            var ability = CgpaToAbility(student.Cgpa);

            decimal totalQualityPoints = 0;
            int totalGpaEcts = 0;
            decimal lastSemQualityPoints = 0;
            int lastSemGpaEcts = 0;

            var entryYear = sp.EnrollmentDate.Year;
            var studentTerms = pastTerms
                .Where(t => t.Year >= entryYear || (t.Year == entryYear - 1 && t.TermType == TermType.Spring))
                .Take(student.CompletedSemesters)
                .ToList();

            // Track passed and failed courses
            var passedCourseIds = new HashSet<int>();
            var failedCourses = new List<int>();

            // Withdrawal scenario: ~5% of borderline students
            bool isBorderline = student.Cgpa >= 2.00m && student.Cgpa < 2.50m;
            bool hasWithdrawn = isBorderline && rng.Next(20) == 0;
            int withdrawSemester = hasWithdrawn ? rng.Next(1, student.CompletedSemesters + 1) : -1;
            bool withdrawUsed = false;

            int currSemester = 0;
            foreach (var term in studentTerms)
            {
                currSemester++;
                lastSemQualityPoints = 0;
                lastSemGpaEcts = 0;

                var semCourses = myCourses
                    .Where(cc => cc.SuggestedSemester == currSemester)
                    .ToList();

                // 1. Retake failed courses from previous semesters
                var retakeList = failedCourses.ToList();
                foreach (var failedCourseId in retakeList)
                {
                    var prereqs = allPrereqs.Where(p => p.CourseId == failedCourseId).ToList();
                    if (prereqs.Any(p => !passedCourseIds.Contains(p.RequiredCourseId)))
                        continue;

                    var course = allCurrCourses.First(cc => cc.CourseId == failedCourseId).Course;
                    GradeCode grade;
                    if (course.IsNonGpa)
                    {
                        grade = GradeCode.S;
                    }
                    else
                    {
                        grade = DrawGrade(ability, rng);
                        var pt = GradePointMap.GetPoint(grade) ?? 0;
                        totalQualityPoints += pt * course.Ects;
                        totalGpaEcts += course.Ects;
                        lastSemQualityPoints += pt * course.Ects;
                        lastSemGpaEcts += course.Ects;
                    }

                    if (grade != GradeCode.FF && grade != GradeCode.NA)
                    {
                        passedCourseIds.Add(failedCourseId);
                        failedCourses.Remove(failedCourseId);
                    }

                    var section = FindOrCreateSection(context, failedCourseId, term.Id,
                        deptInstructors, ref instrIdx, sectionCache);

                    context.CourseRegistrations.Add(new CourseRegistration
                    {
                        StudentId = student.Id,
                        CourseSectionId = section.Id,
                        Status = RegistrationStatus.Completed,
                        Grade = grade,
                        IsRepeated = true,
                        MandatoryAttendance = false,
                        CourseWithdrawalCount = 0,
                        CreatedAt = term.AcademicCalendar?.RegistrationStart ?? term.TermStart,
                        UpdatedAt = term.TermEnd
                    });
                }

                // 2. New courses for this semester
                foreach (var cc in semCourses)
                {
                    // Check prerequisites
                    var prereqs = allPrereqs.Where(p => p.CourseId == cc.CourseId).ToList();
                    if (prereqs.Any(p => !passedCourseIds.Contains(p.RequiredCourseId)))
                        continue;

                    // Withdrawal scenario
                    if (hasWithdrawn && !withdrawUsed && currSemester == withdrawSemester)
                    {
                        withdrawUsed = true;
                        var wSection = FindOrCreateSection(context, cc.CourseId, term.Id,
                            deptInstructors, ref instrIdx, sectionCache);

                        context.CourseRegistrations.Add(new CourseRegistration
                        {
                            StudentId = student.Id,
                            CourseSectionId = wSection.Id,
                            Status = RegistrationStatus.Withdrawn,
                            Grade = GradeCode.W,
                            IsRepeated = false,
                            MandatoryAttendance = false,
                            CourseWithdrawalCount = 1,
                            CreatedAt = term.AcademicCalendar?.RegistrationStart ?? term.TermStart,
                            UpdatedAt = term.TermEnd
                        });
                        student.WithdrawalCount = 1;
                        continue;
                    }

                    GradeCode grade;
                    if (cc.Course.IsNonGpa)
                    {
                        grade = GradeCode.S;
                        passedCourseIds.Add(cc.CourseId);
                    }
                    else
                    {
                        grade = DrawGrade(ability, rng);
                        var pt = GradePointMap.GetPoint(grade) ?? 0;
                        totalQualityPoints += pt * cc.Course.Ects;
                        totalGpaEcts += cc.Course.Ects;
                        lastSemQualityPoints += pt * cc.Course.Ects;
                        lastSemGpaEcts += cc.Course.Ects;

                        if (grade == GradeCode.FF || grade == GradeCode.NA)
                            failedCourses.Add(cc.CourseId);
                        else
                            passedCourseIds.Add(cc.CourseId);
                    }

                    var section = FindOrCreateSection(context, cc.CourseId, term.Id,
                        deptInstructors, ref instrIdx, sectionCache);

                    context.CourseRegistrations.Add(new CourseRegistration
                    {
                        StudentId = student.Id,
                        CourseSectionId = section.Id,
                        Status = RegistrationStatus.Completed,
                        Grade = grade,
                        IsRepeated = false,
                        MandatoryAttendance = false,
                        CourseWithdrawalCount = 0,
                        CreatedAt = term.AcademicCalendar?.RegistrationStart ?? term.TermStart,
                        UpdatedAt = term.TermEnd
                    });
                }
            }

            // Update student GPA from actual grades
            if (totalGpaEcts > 0)
                student.Cgpa = Math.Round(totalQualityPoints / totalGpaEcts, 2);
            if (lastSemGpaEcts > 0)
                student.Sgpa = Math.Round(lastSemQualityPoints / lastSemGpaEcts, 2);

            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Find an existing section with available capacity, or create a new one.
    /// Saves immediately when creating a new section so section.Id is available for FK references.
    /// </summary>
    private static CourseSection FindOrCreateSection(
        AppDbContext context, int courseId, int termId,
        List<Instructor> instructors, ref int instrIdx,
        Dictionary<(int courseId, int termId), List<CourseSection>> sectionCache)
    {
        var key = (courseId, termId);
        int nextSectionNumber = 1;

        if (sectionCache.TryGetValue(key, out var cached))
        {
            var available = cached.FirstOrDefault(s => s.EnrolledCount < s.Capacity);
            if (available != null)
            {
                available.EnrolledCount++;
                return available;
            }
            if (cached.Count > 0)
                nextSectionNumber = cached.Max(s => s.SectionNumber) + 1;
        }
        else
        {
            var dbSections = context.CourseSections
                .Where(s => s.CourseId == courseId && s.TermId == termId)
                .ToList();

            if (dbSections.Count > 0)
            {
                sectionCache[key] = dbSections;
                var available = dbSections.FirstOrDefault(s => s.EnrolledCount < s.Capacity);
                if (available != null)
                {
                    available.EnrolledCount++;
                    return available;
                }
                nextSectionNumber = dbSections.Max(s => s.SectionNumber) + 1;
            }
        }

        // All sections full or none exist — create new.
        // Hoca atanmaz (InstructorId = null) — admin UI'dan elle atayacak.
        // Round-robin instructor atama eskiden burada yapılıyordu, ama bu otomatik
        // programlamanın değil seed'in işiydi ve 'Sıfırla' temizlemiyordu;
        // sezgisel olmadığı için kaldırıldı. instructors/instrIdx parametreleri
        // imzada kaldı (geriye uyumluluk); gelecekte temizlenebilir.

        var section = new CourseSection
        {
            CourseId = courseId,
            TermId = termId,
            SectionNumber = nextSectionNumber,
            InstructorId = null,
            Capacity = 40,
            EnrolledCount = 1,
            Status = CourseSectionStatus.Draft,
            IsFinalized = false
        };
        context.CourseSections.Add(section);
        context.SaveChanges(); // immediate save for FK resolution

        if (!sectionCache.ContainsKey(key))
            sectionCache[key] = [];
        sectionCache[key].Add(section);

        return section;
    }
}
