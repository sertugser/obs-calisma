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

    /// <summary>Etkin dönemde, şubesinde bir öğrenci görünen yeni bir şubeye Active kayıt ekler.</summary>
    public CourseRegistration AktifKayitEkle(Course ders) => KayitEkle(ders, RegistrationStatus.Active);

    /// <summary>Verilen durum ve dönemde (varsayılan: etkin dönem), şubesinde bir öğrenci görünen yeni bir şubeye kayıt ekler.</summary>
    public CourseRegistration KayitEkle(Course ders, RegistrationStatus durum, AcademicTerm? donem = null)
    {
        donem ??= EtkinDonem;
        var sube = SubeEkle(ders, donem);
        sube.EnrolledCount = 1;
        return Kaydet(new CourseRegistration
        {
            Student = Ogrenci, CourseSection = sube, Status = durum, CreatedAt = donem.TermStart
        });
    }

    public void ProgramDuzeyiAyarla(DegreeLevel duzey)
    {
        Db.Set<AcademicProgram>().Single().DegreeLevel = duzey;
        Db.SaveChanges();
    }

    /// <summary>Müfredatın AKTS sınırı alanlarını doldurur; verilmeyen alan boş kalır (boşken üretim varsayılanı geçerli).</summary>
    public void MufredatAktsSiniriAyarla(int? agnoAlti200 = null, int? ara200Ile249 = null, int? ustu250 = null)
    {
        _mufredat.EctsLimitBelow200 = agnoAlti200;
        _mufredat.EctsLimit200To249 = ara200Ile249;
        _mufredat.EctsLimitAbove250 = ustu250;
        Db.SaveChanges();
    }

    /// <summary>Dersi müfredata "kredisiz" açma nedeniyle ekler (talep edilen derste AKTS toplamından düşülür).</summary>
    public CurriculumCourse KredisizMufredataEkle(Course ders) =>
        Kaydet(new CurriculumCourse
        {
            CurriculumId = _mufredat.Id, CourseId = ders.Id, IsCompulsory = true, SuggestedSemester = 1,
            OpeningReason = KredisizNeden()
        });

    public CurriculumCourseOpeningReason KredisizNeden() =>
        Kaydet(new CurriculumCourseOpeningReason { Code = "KS" + Guid.NewGuid().ToString("N")[..6], Name = "Kredisiz", IsCreditFree = true });

    /// <summary>Öğrencinin müfredatından ayrı bir müfredatta (kendi programıyla) dersi "kredisiz" açma nedeniyle tanımlar.</summary>
    public CurriculumCourse BaskaMufredattaKredisizEkle(Course ders)
    {
        var baskaProgram = YeniProgram("BP");
        var baskaMufredat = Kaydet(new Curriculum { Program = baskaProgram, Name = "Başka Müfredat", Version = "2020" });
        return Kaydet(new CurriculumCourse
        {
            CurriculumId = baskaMufredat.Id, CourseId = ders.Id, IsCompulsory = true, SuggestedSemester = 1,
            OpeningReason = KredisizNeden()
        });
    }

    /// <summary>
    /// Mevcut program kaydını verilen tür ve etkinlikle bırakır; öğrenciye, varsayılan sınırlı yeni bir müfredatla etkin bir ana program ekler.
    /// Eski kayıt daha önce eklendiği için süzgeç kalkarsa ilk eşleşen o olur.
    /// </summary>
    public void AnaProgramiDegistir(StudentProgramType eskiTur, bool eskiEtkin)
    {
        var eski = Db.StudentPrograms.Single();
        eski.Type = eskiTur;
        eski.IsActive = eskiEtkin;
        var program = YeniProgram("YP");
        var mufredat = Kaydet(new Curriculum { Program = program, Name = "Yeni Müfredat", Version = "2024" });
        Kaydet(new StudentProgram
        {
            Student = Ogrenci, Program = program, Curriculum = mufredat, Type = StudentProgramType.Main,
            IsActive = true, EnrollmentDate = new DateTime(2024, 9, 15)
        });
    }

    private AcademicProgram YeniProgram(string kod) =>
        Kaydet(new AcademicProgram
        {
            Name = kod + " Programı", Code = kod, Department = Db.Set<Department>().Single(), DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester, MaxStudyDurationSemesters = 8, TotalRequiredEcts = 240
        });

    /// <summary>Başka bir öğrencinin etkin dönemdeki yeni bir şubesine verilen durumda kayıt ekler.</summary>
    public CourseRegistration BaskaOgrenciKayitEkle(Course ders, RegistrationStatus durum)
    {
        var kullanici = Kaydet(new ApplicationUser { UserName = "ogrenci2", FirstName = "Deneme", LastName = "İkinci" });
        var baskasi = Kaydet(new Student
        {
            UserId = kullanici.Id, StudentNumber = "2023002", NationalId = "10000000002", Status = StudentStatus.Active,
            Cgpa = 3.00m, Sgpa = 3.00m, CompletedSemesters = 4
        });
        var sube = SubeEkle(ders, EtkinDonem);
        sube.EnrolledCount = 1;
        return Kaydet(new CourseRegistration { Student = baskasi, CourseSection = sube, Status = durum, CreatedAt = EtkinDonem.TermStart });
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
