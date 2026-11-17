using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.API.Transkript;

/// <summary>
/// Transkript ve AGNO sabitleme testleri (İş paketi 14a) için InMemory veri kurucu: bir program, bir öğrenci,
/// tek hoca. Dönemler ve dersler test içinde sabit tarihlerle eklenir; kimlikleri sağlayıcı üretir.
/// </summary>
internal sealed class TranskriptOrtami : IDisposable
{
    public AppDbContext Db { get; } = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    public Department Bolum { get; } = new() { Name = "Deneme Bölümü", Code = "DB", Faculty = new Faculty { Name = "Deneme Fakültesi", Code = "DF" } };
    public Student Ogrenci { get; }
    private readonly Instructor _hoca;

    public TranskriptOrtami()
    {
        var program = new AcademicProgram
        {
            Name = "Deneme Programı", Code = "DP", Department = Bolum, DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester, MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        };
        var hocaKullanici = Kaydet(new ApplicationUser { UserName = "hoca1", FirstName = "Deneme", LastName = "Hoca" });
        var kullanici = Kaydet(new ApplicationUser { UserName = "ogrenci1", FirstName = "Deneme", LastName = "Öğrenci" });
        Ogrenci = new Student { UserId = kullanici.Id, StudentNumber = "2023001", NationalId = "10000000001", Status = StudentStatus.Active, CompletedSemesters = 2 };
        Db.StudentPrograms.Add(new StudentProgram
        {
            Student = Ogrenci, Program = program, Curriculum = new Curriculum { Program = program, Name = "Deneme Müfredatı", Version = "2023" },
            Type = StudentProgramType.Main, IsActive = true, EnrollmentDate = new DateTime(2023, 9, 15)
        });
        _hoca = Kaydet(new Instructor { UserId = hocaKullanici.Id, Department = Bolum, Title = "Dr.", MaxWeeklyHours = 12 });
    }

    /// <summary>Etkin olmayan (geçmiş) bir dönem ekler; sıralama başlangıç tarihine göre.</summary>
    public AcademicTerm DonemEkle(int yil, TermType tur)
    {
        var baslangic = tur == TermType.Fall ? new DateTime(yil, 10, 1) : new DateTime(yil + 1, 2, 15);
        return Kaydet(new AcademicTerm
        {
            Name = $"{yil}-{yil + 1} {tur}", ShortEnglishName = $"{tur} {yil}", TermType = tur, Year = yil,
            TermStart = baslangic, TermEnd = baslangic.AddMonths(4)
        });
    }

    public Course DersEkle(string kod, int akts, int kredi = 3, bool notsuz = false) =>
        Kaydet(new Course { Code = kod, Name = kod + " Dersi", Ects = akts, NationalCredit = kredi, IsNonGpa = notsuz });

    /// <summary>Ders ve dönem için yeni bir şube açar ve öğrenciyi verilen notla kaydeder.</summary>
    public CourseRegistration KayitEkle(Course ders, AcademicTerm donem, GradeCode? harf,
        RegistrationStatus durum = RegistrationStatus.Completed, Action<CourseRegistration>? ayarla = null)
    {
        var sube = Kaydet(new CourseSection { Course = ders, Term = donem, Instructor = _hoca, SectionNumber = 1, Capacity = 30, Status = CourseSectionStatus.Active });
        var kayit = new CourseRegistration { Student = Ogrenci, CourseSection = sube, Status = durum, Grade = harf, CreatedAt = donem.TermStart };
        ayarla?.Invoke(kayit);
        return Kaydet(kayit);
    }

    public GradeComponent BilesenEkle(Course ders, AcademicTerm donem, string ad, decimal agirlik) =>
        Kaydet(new GradeComponent { CourseId = ders.Id, AcademicTermId = donem.Id, Name = ad, Weight = agirlik, Type = GradeComponentType.Other });

    public StudentGrade PuanEkle(CourseRegistration kayit, GradeComponent bilesen, decimal? puan, decimal? butPuani = null) =>
        Kaydet(new StudentGrade { CourseRegistrationId = kayit.Id, GradeComponentId = bilesen.Id, Score = puan, MakeupScore = butPuani, ExamAttendance = ExamAttendance.Present });

    public void EsdegerEkle(Course eski, Course yeni) =>
        Kaydet(new CourseEquivalency { CourseId = eski.Id, EquivalentCourseId = yeni.Id });

    public GpaCalculationService Gpa() => new(Db, new GradeVisibilityService(Db, new NullWebModuleControlService()));

    /// <summary>Üretimdeki varsayılan görünürlük servisiyle transkript.</summary>
    public async Task<StudentTranscriptDto> TranskriptAsync() =>
        (await new StudentTranscriptService(Db).GetTranscriptAsync(Ogrenci.Id))!;

    public static StudentTranscriptCourseDto Ders(StudentTranscriptDto transkript, AcademicTerm donem, string kod) =>
        transkript.Terms.Single(t => t.TermId == donem.Id).Courses.Single(c => c.CourseCode == kod);

    public T Kaydet<T>(T varlik) where T : class
    {
        Db.Add(varlik);
        Db.SaveChanges();
        return varlik;
    }

    public void Dispose() => Db.Dispose();
}
