using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;

namespace SIS.Tests.Postgres.NotHesabi;

/// <summary>
/// Not hesabı testleri için gerçek PostgreSQL veri kurucu: bir ders, bir dönem (Güz 2024), iki hoca.
/// Her adım kendi context'iyle yazar; testler belirli bir Id'ye dayanmaz.
/// </summary>
internal sealed class NotVeriKurucu(PostgresFixture fixture)
{
    private static readonly DateTime Zaman = new(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    public int DersId { get; private set; }
    public int DonemId { get; private set; }
    public List<(int KullaniciId, int HocaId)> Hocalar { get; } = [];

    public static async Task<NotVeriKurucu> KurAsync(PostgresFixture fixture)
    {
        var veri = new NotVeriKurucu(fixture);
        await using var db = fixture.YeniContext();
        var bolum = new Department { Name = "Deneme Bölümü", Code = "DB", Faculty = new Faculty { Name = "Deneme Fakültesi", Code = "DF" } };
        var ders = new Course { Code = "DEN101", Name = "Deneme Dersi", Ects = 5, NationalCredit = 3, Department = bolum };
        var donem = new AcademicTerm
        {
            Name = "2024-2025 Güz", ShortEnglishName = "Fall 2024", TermType = TermType.Fall, Year = 2024, IsActivated = true,
            TermStart = new DateTime(2024, 10, 1, 0, 0, 0, DateTimeKind.Utc), TermEnd = new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc)
        };
        db.AddRange(ders, donem);
        for (var i = 1; i <= 2; i++)
        {
            var kullanici = new ApplicationUser { UserName = $"hoca{i}", FirstName = "Deneme", LastName = $"Hoca {i}" };
            db.Users.Add(kullanici);
            await db.SaveChangesAsync();
            var hoca = new Instructor { UserId = kullanici.Id, Department = bolum, Title = "Dr.", MaxWeeklyHours = 12 };
            db.Instructors.Add(hoca);
            await db.SaveChangesAsync();
            veri.Hocalar.Add((kullanici.Id, hoca.Id));
        }
        veri.DersId = ders.Id;
        veri.DonemId = donem.Id;
        return veri;
    }

    public async Task<int> SubeEkleAsync(int hoca, int no, bool koordinator = false) => (await KaydetAsync(new CourseSection
    {
        CourseId = DersId, TermId = DonemId, InstructorId = Hocalar[hoca].HocaId, SectionNumber = no, Capacity = 30,
        Status = CourseSectionStatus.Active, IsExamCoordinator = koordinator
    })).Id;

    public async Task<int> BilesenEkleAsync(string ad, decimal agirlik, GradeComponentType tur, int sira) => (await KaydetAsync(new GradeComponent
    {
        CourseId = DersId, AcademicTermId = DonemId, Name = ad, Weight = agirlik, Type = tur, Order = sira, PublishedAt = Zaman
    })).Id;

    /// <summary>Varsayılan eşikli (91…45) harf ölçütü.</summary>
    public Task HarfOlcutuEkleAsync() => KaydetAsync(new GradingCriteria { CourseId = DersId, AcademicTermId = DonemId });

    /// <summary>Öğrenci ve şubeye aktif ders kaydı; ortalamalar verilen başlangıç değeriyle.</summary>
    public async Task<(int OgrenciId, int KayitId)> OgrenciEkleAsync(int subeId, string no, decimal ortalama = 0m)
    {
        var kullanici = await KaydetAsync(new ApplicationUser { UserName = "ogrenci" + no, FirstName = "Deneme", LastName = "Öğrenci" });
        var ogrenci = await KaydetAsync(new Student
        {
            UserId = kullanici.Id, StudentNumber = "2024" + no, NationalId = "1000000000" + no, Status = StudentStatus.Active,
            CompletedSemesters = 1, Cgpa = ortalama, Sgpa = ortalama, CreatedAt = Zaman
        });
        var kayit = await KaydetAsync(new CourseRegistration { StudentId = ogrenci.Id, CourseSectionId = subeId, Status = RegistrationStatus.Active, CreatedAt = Zaman });
        return (ogrenci.Id, kayit.Id);
    }

    public Task PuanEkleAsync(int kayitId, int bilesenId, decimal puan) => KaydetAsync(new StudentGrade
    {
        CourseRegistrationId = kayitId, GradeComponentId = bilesenId, Score = puan, ExamAttendance = ExamAttendance.Present
    });

    /// <summary>Verilen context'le, verilen hocanın kimliğiyle controller. GPA servisi verilmezse gerçek servis.</summary>
    public GradeController Controller(AppDbContext db, int hoca = 0, IGpaCalculationService? gpa = null) =>
        new(db, gpa ?? GercekGpa(db), new SystemDateTimeProvider(), new ExamRegulationResolver(db),
            webModuleControls: new NullWebModuleControlService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, Hocalar[hoca].KullaniciId.ToString())], "TestAuth"))
                }
            }
        };

    public static GpaCalculationService GercekGpa(AppDbContext db) =>
        new(db, new GradeVisibilityService(db, new NullWebModuleControlService()));

    /// <summary>Controller'ın anonim yanıt nesnesinden bir özelliği okur.</summary>
    public static T Oku<T>(object? nesne, string ad) => (T)nesne!.GetType().GetProperty(ad)!.GetValue(nesne)!;

    private async Task<T> KaydetAsync<T>(T varlik) where T : class
    {
        await using var db = fixture.YeniContext();
        db.Add(varlik);
        await db.SaveChangesAsync();
        return varlik;
    }
}
