using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.Postgres.Yetki;

/// <summary>
/// Öğrenci ve danışman kapsamı testleri için gerçek PostgreSQL veri kurucu: bir bölümde lisans programı, müfredat ve
/// müfredatta iki ders. Öğretim elemanı, öğrenci, ders, şube ve kayıt kimlikleri birbirinden ve verilen kullanıcı
/// kimliklerinden ayrıktır (denk gelen kayıt silinir, bir sonrakiyle yeniden denenir). Tarihler sabittir; seed yok.
/// </summary>
internal sealed class KapsamVeriKurucu(PostgresFixture fixture)
{
    private static readonly DateTime Tarih = new(2045, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly HashSet<int> _kullanilanKimlikler = [];
    private int _bolumId, _programId, _mufredatId, _ogrenciSayaci;

    public int[] DersIdleri { get; private set; } = [];

    public int BolumId => _bolumId;

    public int ProgramId => _programId;

    /// <summary>Varlık kurmadan: verilen kimlikler (kullanıcı, fakülte, dönem vb.) sonraki varlıkların ayrıştırılacağı kümeye girer.</summary>
    public static KapsamVeriKurucu Ayristirici(PostgresFixture fixture, params int[] kimlikler)
    {
        var veri = new KapsamVeriKurucu(fixture);
        veri._kullanilanKimlikler.UnionWith(kimlikler);
        return veri;
    }

    /// <summary>Bölümsüz yeni fakülte; kimliği ayrık.</summary>
    public async Task<int> FakulteAsync(string kod) =>
        (await AyrikKaydetAsync(() => new Faculty { Name = kod + " Fakültesi", Code = kod }, f => f.Id)).Id;

    /// <summary>Verilen fakültede yeni bölüm; kimliği ayrık.</summary>
    public async Task<int> BolumAsync(int fakulteId, string kod) =>
        (await AyrikKaydetAsync(() => new Department { Name = kod + " Bölümü", Code = kod, FacultyId = fakulteId }, d => d.Id)).Id;

    /// <summary>Kullanıcıya bağlı, verilen bölümdeki öğretim elemanı; kimliği ayrık.</summary>
    public async Task<int> OgretimElemaniAsync(int kullaniciId, int bolumId) => (await AyrikKaydetAsync(() => new Instructor
    {
        UserId = kullaniciId, DepartmentId = bolumId, Title = "Dr.", MaxWeeklyHours = 20, CreatedAt = Tarih
    }, i => i.Id)).Id;

    /// <summary>Kullanıcılar önceden oluşturulur; kimlikleri sonraki varlıkların ayrıştırılacağı kümeye girer.</summary>
    public static async Task<KapsamVeriKurucu> KurAsync(PostgresFixture fixture, params int[] kullaniciIdleri)
    {
        var veri = new KapsamVeriKurucu(fixture);
        veri._kullanilanKimlikler.UnionWith(kullaniciIdleri);
        veri._bolumId = (await veri.KaydetAsync(new Department
        {
            Name = "Kapsam Bölümü", Code = "KPB", Faculty = new Faculty { Name = "Kapsam Fakültesi", Code = "KPF" }
        })).Id;
        veri._programId = (await veri.KaydetAsync(new AcademicProgram
        {
            Name = "Kapsam Programı", Code = "KPP", DepartmentId = veri._bolumId, DegreeLevel = DegreeLevel.Bachelor,
            EducationType = EducationType.Semester, MaxStudyDurationSemesters = 14, TotalRequiredEcts = 240, CreatedAt = Tarih
        })).Id;
        veri._mufredatId = (await veri.KaydetAsync(new Curriculum
        {
            ProgramId = veri._programId, Version = "2045", Name = "Kapsam Müfredatı", IsActive = true,
            Status = CurriculumStatus.Published, CreatedAt = Tarih
        })).Id;
        var dersler = new List<int>();
        foreach (var (kod, donem) in new[] { ("KPS101", 1), ("KPS102", 2) })
        {
            var ders = await veri.AyrikKaydetAsync(() => new Course
            {
                Code = kod, Name = kod + " Dersi", Ects = 5, NationalCredit = 3, TheoryHours = 3, DepartmentId = veri._bolumId
            }, d => d.Id);
            await veri.KaydetAsync(new CurriculumCourse
            {
                CurriculumId = veri._mufredatId, CourseId = ders.Id, IsCompulsory = true, SuggestedSemester = donem
            });
            dersler.Add(ders.Id);
        }

        veri.DersIdleri = [.. dersler];
        return veri;
    }

    /// <summary>Kullanıcıya bağlı, bölümdeki öğretim elemanı; kimliğini döndürür.</summary>
    public async Task<int> OgretimElemaniAsync(int kullaniciId) => (await AyrikKaydetAsync(() => new Instructor
    {
        UserId = kullaniciId, DepartmentId = _bolumId, Title = "Dr.", MaxWeeklyHours = 20, CreatedAt = Tarih
    }, i => i.Id)).Id;

    /// <summary>Kullanıcıya bağlı etkin öğrenci (4 yarıyıl tamamlamış), programın ana satırıyla; kimliğini döndürür.</summary>
    public async Task<int> OgrenciAsync(int kullaniciId, int? danismanId = null)
    {
        var no = (++_ogrenciSayaci).ToString("D3");
        var ogrenciId = (await AyrikKaydetAsync(() => new Student
        {
            UserId = kullaniciId, StudentNumber = "2045" + no, NationalId = "20000000" + no, Status = StudentStatus.Active,
            CompletedSemesters = 4, Cgpa = 3.00m, Sgpa = 3.00m, AdvisorId = danismanId, CreatedAt = Tarih
        }, s => s.Id)).Id;
        await KaydetAsync(new StudentProgram
        {
            StudentId = ogrenciId, ProgramId = _programId, CurriculumId = _mufredatId, Type = StudentProgramType.Main,
            EnrollmentDate = Tarih, IsActive = true
        });
        return ogrenciId;
    }

    /// <summary>
    /// Etkin olmayan bir dönemde iki dersin birer şubesi ve her öğrencinin iki şubede birer Active kaydı;
    /// dersten çekilme modülü web ayarıyla "open". Dönüş: (şube kimlikleri, öğrenci sırasıyla kayıt kimlikleri).
    /// </summary>
    public async Task<(int[] Subeler, int[][] Kayitlar)> CekilmeKurAsync(params int[] ogrenciIdleri)
    {
        var donemId = (await KaydetAsync(new AcademicTerm
        {
            Name = "2045-2046 Güz", ShortEnglishName = "Fall 2045", TermType = TermType.Fall, Year = 2045,
            TermStart = new DateTime(2045, 9, 15, 0, 0, 0, DateTimeKind.Utc), TermEnd = new DateTime(2046, 1, 15, 0, 0, 0, DateTimeKind.Utc)
        })).Id;
        var subeler = new List<int>();
        foreach (var dersId in DersIdleri)
        {
            subeler.Add((await AyrikKaydetAsync(() => new CourseSection
            {
                CourseId = dersId, TermId = donemId, SectionNumber = 1, Capacity = 30, EnrolledCount = ogrenciIdleri.Length,
                Status = CourseSectionStatus.Active, CreatedAt = Tarih
            }, s => s.Id)).Id);
        }

        var kayitlar = new List<int[]>();
        foreach (var ogrenciId in ogrenciIdleri)
        {
            var satir = new List<int>();
            foreach (var subeId in subeler)
            {
                satir.Add((await AyrikKaydetAsync(() => new CourseRegistration
                {
                    StudentId = ogrenciId, CourseSectionId = subeId, Status = RegistrationStatus.Active, CreatedAt = Tarih
                }, r => r.Id)).Id);
            }

            kayitlar.Add([.. satir]);
        }

        await KaydetAsync(new WebModuleSetting
        {
            ModuleKey = "StudentWithdrawal", Key = "Mode", Value = "open", Category = "StudentWithdrawal", DataType = "string"
        });
        return ([.. subeler], [.. kayitlar]);
    }

    private async Task<T> AyrikKaydetAsync<T>(Func<T> uret, Func<T, int> kimlik) where T : class
    {
        while (true)
        {
            var varlik = await KaydetAsync(uret());
            if (_kullanilanKimlikler.Add(kimlik(varlik)))
                return varlik;
            await using var db = fixture.YeniContext();
            db.Remove(varlik);
            await db.SaveChangesAsync();
        }
    }

    private async Task<T> KaydetAsync<T>(T varlik) where T : class
    {
        await using var db = fixture.YeniContext();
        db.Add(varlik);
        await db.SaveChangesAsync();
        return varlik;
    }
}
