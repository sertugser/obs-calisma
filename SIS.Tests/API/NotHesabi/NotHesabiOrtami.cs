using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Application.GpaCalculation.DTOs;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;

namespace SIS.Tests.API.NotHesabi;

/// <summary>
/// Not hesabı sabitleme testleri (İş paketi 13a) için InMemory veri kurucu: bir program, bir ders,
/// bir dönem (Güz 2024), tek hocalı bir şube. Kimlikleri sağlayıcı üretir; testler belirli bir Id'ye dayanmaz.
/// </summary>
internal sealed class NotHesabiOrtami : IDisposable
{
    public AppDbContext Db { get; } = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    public AcademicProgram Program { get; }
    public Curriculum Mufredat { get; }
    public Course Ders { get; } = new() { Code = "DEN101", Name = "Deneme Dersi", Ects = 5 };
    public AcademicTerm Donem { get; } = new()
    {
        Name = "2024-2025 Güz", ShortEnglishName = "Fall 2024", TermType = TermType.Fall, Year = 2024,
        IsActivated = true, TermStart = new DateTime(2024, 10, 1), TermEnd = new DateTime(2025, 1, 31)
    };
    public ApplicationUser HocaKullanici { get; } = new() { UserName = "hoca1", FirstName = "Deneme", LastName = "Hoca" };
    public CourseSection Sube { get; }

    public NotHesabiOrtami()
    {
        var bolum = new Department { Name = "Deneme Bölümü", Code = "DB", Faculty = new Faculty { Name = "Deneme Fakültesi", Code = "DF" } };
        Program = new AcademicProgram
        {
            Name = "Deneme Programı", Code = "DP", Department = bolum, DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester, MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        };
        Mufredat = new Curriculum { Program = Program, Name = "Deneme Müfredatı", Version = "2024" };
        Db.CurriculumCourses.Add(new CurriculumCourse { Curriculum = Mufredat, Course = Ders, IsCompulsory = true, SuggestedSemester = 1 });
        Db.AcademicTerms.Add(Donem);
        Db.Users.Add(HocaKullanici);
        Db.SaveChanges();

        var hoca = new Instructor { UserId = HocaKullanici.Id, Department = bolum, Title = "Dr.", MaxWeeklyHours = 12 };
        Sube = new CourseSection { Course = Ders, Term = Donem, Instructor = hoca, SectionNumber = 1, Capacity = 30, Status = CourseSectionStatus.Active };
        Db.CourseSections.Add(Sube);
        Db.SaveChanges();
    }

    public GradeComponent BilesenEkle(string ad, decimal agirlik, GradeComponentType tur, int sira)
    {
        var bilesen = new GradeComponent
        {
            CourseId = Ders.Id, AcademicTermId = Donem.Id, Name = ad, Weight = agirlik, Type = tur, Order = sira,
            PublishedAt = new DateTime(2024, 11, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        return Kaydet(bilesen);
    }

    /// <summary>Şubeye varsayılan eşikli (91…45) harf ölçütü ekler.</summary>
    public void HarfOlcutuEkle() => Kaydet(new GradingCriteria { CourseId = Ders.Id, AcademicTermId = Donem.Id });

    public void YonergeEkle(Action<ExamRegulation> ayarla)
    {
        var yonerge = new ExamRegulation
        {
            ProgramId = Program.Id, DegreeLevel = DegreeLevel.Bachelor, TermType = TermType.Fall, Year = 2024,
            Name = "Deneme Yönergesi", CreatedAt = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        ayarla(yonerge);
        Kaydet(yonerge);
    }

    /// <summary>Öğrenci, ana program kaydı ve şubeye aktif ders kaydı oluşturur.</summary>
    public (Student Ogrenci, ApplicationUser Kullanici, CourseRegistration Kayit) OgrenciEkle(string no)
    {
        var kullanici = Kaydet(new ApplicationUser { UserName = "ogrenci" + no, FirstName = "Deneme", LastName = "Öğrenci" });
        var ogrenci = new Student
        {
            UserId = kullanici.Id, StudentNumber = "2024" + no, NationalId = "1000000000" + no,
            Status = StudentStatus.Active, CompletedSemesters = 1
        };
        Db.StudentPrograms.Add(new StudentProgram
        {
            Student = ogrenci, Program = Program, Curriculum = Mufredat, Type = StudentProgramType.Main,
            IsActive = true, EnrollmentDate = new DateTime(2024, 9, 15)
        });
        var kayit = Kaydet(new CourseRegistration { Student = ogrenci, CourseSection = Sube, Status = RegistrationStatus.Active });
        return (ogrenci, kullanici, kayit);
    }

    public void PuanEkle(CourseRegistration kayit, GradeComponent bilesen, decimal? puan, ExamAttendance katilim = ExamAttendance.Present) =>
        Kaydet(new StudentGrade { CourseRegistrationId = kayit.Id, GradeComponentId = bilesen.Id, Score = puan, ExamAttendance = katilim });

    public GradeController NotController(IWebModuleControlService? pencereler = null)
    {
        var gpa = new Mock<IGpaCalculationService>();
        gpa.Setup(g => g.RecalculateAndUpdateAsync(It.IsAny<int>())).ReturnsAsync(new GpaResultDto());
        return new GradeController(Db, gpa.Object, new SystemDateTimeProvider(), new ExamRegulationResolver(Db),
            webModuleControls: pencereler ?? new NullWebModuleControlService())
        {
            ControllerContext = KullaniciBaglami(HocaKullanici.Id)
        };
    }

    public static ControllerContext KullaniciBaglami(int userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth"))
        }
    };

    /// <summary>Controller'ın anonim yanıt nesnesinden bir özelliği okur.</summary>
    public static T Oku<T>(object nesne, string ad) => (T)nesne.GetType().GetProperty(ad)!.GetValue(nesne)!;

    private T Kaydet<T>(T varlik) where T : class
    {
        Db.Add(varlik);
        Db.SaveChanges();
        return varlik;
    }

    public void Dispose() => Db.Dispose();
}

/// <summary>Kod bazında pencere durumu döndüren saplama; listede olmayan kod kapalı sayılır.</summary>
internal sealed class SaplamaPencereServisi(IDictionary<string, WebModuleAccessResult> durumlar) : IWebModuleControlService
{
    public WebModuleControlDefinition? GetDefinition(string code) => null;

    public Task<IReadOnlyList<WebModuleControlSummary>> GetSummariesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WebModuleControlSummary>>([]);

    public Task<WebModuleAccessResult> EvaluateAsync(string code, int? termId = null, CancellationToken ct = default)
        => Task.FromResult(durumlar.TryGetValue(code, out var durum) ? durum : Kapali(WebModuleControlModes.Closed));

    public static WebModuleAccessResult Acik() => new(true, true, WebModuleControlModes.Open, "Açık", "Açık", null);

    public static WebModuleAccessResult Kapali(string mod) => new(true, false, mod, "Kapalı", "Kapalı", "Pencere kapalı.");
}
