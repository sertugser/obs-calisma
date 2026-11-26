using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;
using SIS.Tests.Postgres.NotHesabi;

namespace SIS.Tests.Postgres.Transkript;

// D-04 (PostgreSQL): transkript ders ortalamasının puan süzgeci (Score dolu ya da MakeupScore dolu) gerçek SQL'e çevriliyor.
// Kural ve karşı örnekler SIS.Tests'teki D04_ButunlemeOrtalamasiTests ile sabit.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class D04_ButunlemeOrtalamasiTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task D04_FinalPuaniBos_Butunleme80_OrtalamaHesaplaniyor()
    {
        var veri = await NotVeriKurucu.KurAsync(fixture);
        var vize = await veri.BilesenEkleAsync("Vize", 40m, GradeComponentType.Midterm, 1);
        var final = await veri.BilesenEkleAsync("Final", 60m, GradeComponentType.Final, 2);
        var (ogrenciId, kayitId) = await veri.OgrenciEkleAsync(await veri.SubeEkleAsync(0, 1), "401");
        await using (var db = fixture.YeniContext())
        {
            var bolumId = await db.Courses.Where(c => c.Id == veri.DersId).Select(c => c.DepartmentId!.Value).SingleAsync();
            var program = new AcademicProgram
            {
                Name = "Deneme Programı", Code = "D04P", DepartmentId = bolumId, DegreeLevel = DegreeLevel.Bachelor,
                EducationType = EducationType.Semester, MaxStudyDurationSemesters = 14, TotalRequiredEcts = 240
            };
            db.StudentPrograms.Add(new StudentProgram
            {
                StudentId = ogrenciId, Program = program, Curriculum = new Curriculum { Program = program, Version = "2024" },
                Type = StudentProgramType.Main, IsActive = true, EnrollmentDate = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            await db.CourseRegistrations.Where(r => r.Id == kayitId).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RegistrationStatus.Completed).SetProperty(r => r.Grade, GradeCode.CC));
            db.StudentGrades.AddRange(
                new StudentGrade { CourseRegistrationId = kayitId, GradeComponentId = vize, Score = 60m, ExamAttendance = ExamAttendance.Present },
                new StudentGrade { CourseRegistrationId = kayitId, GradeComponentId = final, Score = null, MakeupScore = 80m, ExamAttendance = ExamAttendance.Present });
            await db.SaveChangesAsync();
        }

        await using var okuma = fixture.YeniContext();
        var transkript = await new StudentTranscriptService(okuma).GetTranscriptAsync(ogrenciId);

        // 60×0,40 + 80×0,60 = 24 + 48 = 72,00 (Score boş satır elenseydi null)
        Assert.Equal(72.00m, Assert.Single(Assert.Single(transkript!.Terms).Courses).AverageScore);
    }
}
