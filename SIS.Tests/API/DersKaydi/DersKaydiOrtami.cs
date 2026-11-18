using Microsoft.EntityFrameworkCore;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.API.DersKaydi;

/// <summary>
/// Ders kaydı ve ön koşul sabitleme testleri (İş paketi 15a) için InMemory veri kurucu: bir program, bir öğrenci,
/// tek hoca, geçmiş bir dönem ve kayıt penceresi açık bir etkin dönem. Saat sabittir (<see cref="Saat"/>);
/// derslerin bölümü yoktur, bu yüzden yayın kapısı yalnız şube durumuna bakar.
/// </summary>
internal sealed class DersKaydiOrtami : IDisposable
{
    public AppDbContext Db { get; } = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    public FakeClock Saat { get; } = new() { UtcNow = new DateTime(2025, 3, 10, 12, 0, 0, DateTimeKind.Utc) };
    public Student Ogrenci { get; }
    public AcademicTerm GecmisDonem { get; }
    public AcademicTerm OncekiDonem { get; }
    public AcademicTerm EtkinDonem { get; }
    private readonly Instructor _hoca;
    private readonly Curriculum _mufredat;

    public DersKaydiOrtami(int tamamlananDonem = 4, decimal agno = 3.00m)
    {
        var bolum = new Department { Name = "Deneme Bölümü", Code = "DB", Faculty = new Faculty { Name = "Deneme Fakültesi", Code = "DF" } };
        var program = new AcademicProgram
        {
            Name = "Deneme Programı", Code = "DP", Department = bolum, DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester, MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        };
        _mufredat = new Curriculum { Program = program, Name = "Deneme Müfredatı", Version = "2023" };
        var hocaKullanici = Kaydet(new ApplicationUser { UserName = "hoca1", FirstName = "Deneme", LastName = "Hoca" });
        var kullanici = Kaydet(new ApplicationUser { UserName = "ogrenci1", FirstName = "Deneme", LastName = "Öğrenci" });
        Ogrenci = new Student
        {
            UserId = kullanici.Id, StudentNumber = "2023001", NationalId = "10000000001", Status = StudentStatus.Active,
            Cgpa = agno, Sgpa = agno, CompletedSemesters = tamamlananDonem
        };
        Db.StudentPrograms.Add(new StudentProgram
        {
            Student = Ogrenci, Program = program, Curriculum = _mufredat, Type = StudentProgramType.Main,
            IsActive = true, EnrollmentDate = new DateTime(2023, 9, 15)
        });
        _hoca = Kaydet(new Instructor { UserId = hocaKullanici.Id, Department = bolum, Title = "Dr.", MaxWeeklyHours = 12 });
        GecmisDonem = Kaydet(new AcademicTerm
        {
            Name = "2024-2025 Fall", ShortEnglishName = "Fall 2024", TermType = TermType.Fall, Year = 2024,
            TermStart = new DateTime(2024, 10, 1), TermEnd = new DateTime(2025, 1, 31)
        });
        OncekiDonem = Kaydet(new AcademicTerm
        {
            Name = "2023-2024 Fall", ShortEnglishName = "Fall 2023", TermType = TermType.Fall, Year = 2023,
            TermStart = new DateTime(2023, 10, 1), TermEnd = new DateTime(2024, 1, 31)
        });
        EtkinDonem = Kaydet(new AcademicTerm
        {
            Name = "2024-2025 Spring", ShortEnglishName = "Spring 2024", TermType = TermType.Spring, Year = 2024,
            TermStart = new DateTime(2025, 2, 15), TermEnd = new DateTime(2025, 6, 15), IsActivated = true,
            AcademicCalendar = new TermAcademicCalendar
            {
                RegistrationStart = new DateTime(2025, 3, 1), RegistrationEnd = new DateTime(2025, 3, 20),
                AddDropStart = new DateTime(2025, 3, 21), AddDropEnd = new DateTime(2025, 3, 28),
                WithdrawalDeadline = new DateTime(2025, 5, 10),
                GradeEntryStart = new DateTime(2025, 6, 1), GradeEntryEnd = new DateTime(2025, 6, 29),
                CreatedAt = new DateTime(2025, 1, 1)
            }
        });
    }

    public Course DersEkle(string kod, int akts = 5) =>
        Kaydet(new Course { Code = kod, Name = kod + " Dersi", Ects = akts, NationalCredit = 3 });

    public void OnKosulEkle(Course ders, Course gerekli, bool esKosul = false) =>
        Kaydet(new CoursePrerequisite { CourseId = ders.Id, RequiredCourseId = gerekli.Id, IsCorequisite = esKosul });

    public void EsdegerEkle(Course bir, Course iki) =>
        Kaydet(new CourseEquivalency { CourseId = bir.Id, EquivalentCourseId = iki.Id });

    public void MufredataEkle(Course ders, int onerilenDonem) =>
        Kaydet(new CurriculumCourse { CurriculumId = _mufredat.Id, CourseId = ders.Id, IsCompulsory = true, SuggestedSemester = onerilenDonem });

    /// <summary>Dersi geçmiş dönemde verilen notla tamamlanmış sayar.</summary>
    public CourseRegistration NotEkle(Course ders, GradeCode harf, AcademicTerm? donem = null)
    {
        donem ??= GecmisDonem;
        return Kaydet(new CourseRegistration
        {
            Student = Ogrenci, CourseSection = SubeEkle(ders, donem), Status = RegistrationStatus.Completed,
            Grade = harf, CreatedAt = donem.TermStart
        });
    }

    /// <summary>Etkin dönemde açık bir şube ekler.</summary>
    public CourseSection Sube(Course ders) => SubeEkle(ders, EtkinDonem);

    private CourseSection SubeEkle(Course ders, AcademicTerm donem) =>
        Kaydet(new CourseSection { Course = ders, Term = donem, Instructor = _hoca, SectionNumber = 1, Capacity = 30, Status = CourseSectionStatus.Active });

    /// <summary>Etkin dönemde, şubesinde <paramref name="kayitli"/> öğrenci görünen yeni bir şubeye Active kayıt ekler.</summary>
    public CourseRegistration AktifKayitEkle(Course ders, int kayitli = 1)
    {
        var sube = Sube(ders);
        sube.EnrolledCount = kayitli;
        return Kaydet(new CourseRegistration
        {
            Student = Ogrenci, CourseSection = sube, Status = RegistrationStatus.Active, CreatedAt = EtkinDonem.TermStart
        });
    }

    public void ProgramDuzeyiAyarla(DegreeLevel duzey)
    {
        Db.Set<AcademicProgram>().Single().DegreeLevel = duzey;
        Db.SaveChanges();
    }

    public GpaCalculationService Gpa() => new(Db, new GradeVisibilityService(Db, new NullWebModuleControlService()));

    public RegistrationValidationService Dogrulama() => new(Db, Saat);

    public CourseRegistrationService Kayit() => new(Db, Dogrulama(), Saat);

    public StudentRegistrationCatalogService Katalog() => new(Db, new EctsLimitResolver(new NullSystemConfigProvider()),
        new RegistrationGate(Db), new ElectivePoolProgressCalculator(Db), Saat, new NullWebModuleControlService());

    public async Task<StudentRegistrationCatalogDto> KatalogAsync() =>
        await Katalog().BuildAsync(Ogrenci, Db.StudentPrograms.Single(sp => sp.StudentId == Ogrenci.Id));

    public static CatalogCourseRowDto Satir(StudentRegistrationCatalogDto katalog, string kod) =>
        katalog.ClassYears.SelectMany(y => y.Semesters).SelectMany(s => s.Courses).Single(c => c.Code == kod);

    public T Kaydet<T>(T varlik) where T : class
    {
        Db.Add(varlik);
        Db.SaveChanges();
        return varlik;
    }

    public void Dispose() => Db.Dispose();
}
