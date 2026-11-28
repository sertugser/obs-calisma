using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Application.Scheduling.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scheduling;

namespace SIS.Tests.API.DersProgrami;

// D35b: çok bloklu takasta satırlar kronolojik sırayla (gün, başlangıç saati) eşlenir ve blok türü şubenin kendi bloğunda kalır.
// Gerçek controller ve gerçek SchedulingService (gerçek kural motoru). Her şubenin iki program satırı var; satır derslikleri birbirinden
// ve şubenin ana dersliğinden farklı. Kimlikler varlık türleri arasında ayrık: derslik 11-17, zaman dilimi 21-25, şube 31-34, ders 41-44,
// hoca 51-54, program satırı 61-62 (S1), 71-72 (S2), 81 (S3), 91 (S4).
// Zaman dilimleri: Z1 Pzt 09, Z2 Pzt 10, Z3 Sal 09, Z4 Sal 10, Z5 Çar 09. Arka plan şubesi S4 (Z5@D7) programa üçüncü bir gün
// katar; böylece iki gün ders veren hocalar boş gün kuralına (H-DO, günler atanmış bloklardan çıkarılır) takılmaz. S1: Z1@D1, Z3@D3 (ana D5). S2: Z2@D2, Z4@D4 (ana D6).
public sealed class D35b_CokBlokluTakasTests : IDisposable
{
    private const int D1 = 11, D2 = 12, D3 = 13, D4 = 14, D5 = 15, D6 = 16, D7 = 17;
    private const int Z1 = 21, Z2 = 22, Z3 = 23, Z4 = 24, Z5 = 25;
    private const int S1 = 31, S2 = 32, S3 = 33, S4 = 34;

    private const ScheduleBlockType T = ScheduleBlockType.Theory, L = ScheduleBlockType.Lab;

    private readonly string _vt = Guid.NewGuid().ToString();
    private readonly AppDbContext _db;

    public D35b_CokBlokluTakasTests() => _db = YeniContext();

    public void Dispose() => _db.Dispose();

    // Üç kip; her satır kendi sırasındaki eşiyle eşlenir. Yalnız zaman dilimi takası satır dersliklerine ve ana dersliğe dokunmaz.
    [Theory]
    [InlineData(true, false, Z2, D1, Z4, D3, Z1, D2, Z3, D4, D5, D6)]
    [InlineData(false, true, Z1, D2, Z3, D4, Z2, D1, Z4, D3, D6, D5)]
    [InlineData(true, true, Z2, D2, Z4, D4, Z1, D1, Z3, D3, D6, D5)]
    public async Task D35b_CokBlok_Takas200_SatirDerslikleriKronolojikEslenir(bool slot, bool derslik,
        int s1z1, int s1d1, int s1z2, int s1d2, int s2z1, int s2d1, int s2z2, int s2d2, int s1Ana, int s2Ana)
    {
        Kur();

        var sonuc = await TakasAsync(slot, derslik);

        OnaylandiMi(sonuc);
        Assert.Equal(new[] { (s1z1, s1d1, T), (s1z2, s1d2, T) }, await ProgramAsync(S1));
        Assert.Equal(new[] { (s2z1, s2d1, T), (s2z2, s2d2, T) }, await ProgramAsync(S2));
        Assert.Equal(new[] { (S1, s1Ana), (S2, s2Ana), (S4, D7) }, await AnaDersliklerAsync());
    }

    // Üçüncü şube S1'in takas sonrası satırının gerçek dersliğinde: çakışma, satır dersliğiyle denetlenir (ana derslikle değil).
    [Theory]
    [InlineData(true, false, Z2, D1)]
    [InlineData(false, true, Z1, D2)]
    public async Task D35b_UcuncuSubeYeniSatirDersliginde_Takas409(bool slot, bool derslik, int s3Dilim, int s3Derslik)
    {
        Kur(ucuncu: (s3Dilim, s3Derslik));

        var sonuc = await TakasAsync(slot, derslik);

        var cakisma = Assert.IsType<ConflictObjectResult>(sonuc);
        var cakismalar = (IEnumerable<SchedulingConflictDto>)cakisma.Value!.GetType().GetProperty("conflicts")!.GetValue(cakisma.Value)!;
        Assert.Contains(cakismalar, c => c.SectionId == S1 && c.RelatedSectionId == S3);
        Assert.Equal(new[] { (Z1, D1, T), (Z3, D3, T) }, await ProgramAsync(S1));
        Assert.Equal(new[] { (Z2, D2, T), (Z4, D4, T) }, await ProgramAsync(S2));
        Assert.Equal(new[] { (S1, D5), (S2, D6), (S3, s3Derslik), (S4, D7) }, await AnaDersliklerAsync());
    }

    // Üçüncü şube S1'in ana dersliğinde ve yeni saatinde: S1'in satırı orada değil, sahte çakışma yok.
    [Fact]
    public async Task D35b_UcuncuSubeAnaDerslikte_Takas200()
    {
        Kur(ucuncu: (Z2, D5));

        var sonuc = await TakasAsync(slot: true, derslik: false);

        OnaylandiMi(sonuc);
        Assert.Equal(new[] { (Z2, D1, T), (Z4, D3, T) }, await ProgramAsync(S1));
        Assert.Equal(new[] { (Z1, D2, T), (Z3, D4, T) }, await ProgramAsync(S2));
    }

    // Bir şubenin satırları hem kimlik hem ekleme sırasıyla ters kurulur (geç saatteki satır önce ve küçük kimlikle). Eşleme yine kronolojik.
    [Theory]
    [InlineData(true, false, S1, Z2, D1, Z4, D3, Z1, D2, Z3, D4)]
    [InlineData(true, false, S2, Z2, D1, Z4, D3, Z1, D2, Z3, D4)]
    [InlineData(false, true, S1, Z1, D2, Z3, D4, Z2, D1, Z4, D3)]
    [InlineData(false, true, S2, Z1, D2, Z3, D4, Z2, D1, Z4, D3)]
    public async Task D35b_SatirlarSirasizDoner_EslemeKronolojik(bool slot, bool derslik, int tersSube,
        int s1z1, int s1d1, int s1z2, int s1d2, int s2z1, int s2d1, int s2z2, int s2d2)
    {
        Kur(tersSube: tersSube);

        var sonuc = await TakasAsync(slot, derslik);

        OnaylandiMi(sonuc);
        Assert.Equal(new[] { (s1z1, s1d1, T), (s1z2, s1d2, T) }, await ProgramAsync(S1));
        Assert.Equal(new[] { (s2z1, s2d1, T), (s2z2, s2d2, T) }, await ProgramAsync(S2));
    }

    // S1'in dersi 1 teori + 1 uygulama (Z1 satırı Theory, Z3 satırı Lab); S2'nin dersi 2 teori. Tür her kipte şubenin kendi bloğunda kalır.
    [Theory]
    [InlineData(true, false, Z2, D1, Z4, D3, Z1, D2, Z3, D4)]
    [InlineData(false, true, Z1, D2, Z3, D4, Z2, D1, Z4, D3)]
    [InlineData(true, true, Z2, D2, Z4, D4, Z1, D1, Z3, D3)]
    public async Task D35b_BlokTuruKorunur(bool slot, bool derslik,
        int s1z1, int s1d1, int s1z2, int s1d2, int s2z1, int s2d1, int s2z2, int s2d2)
    {
        Kur(s1Uygulamali: true);

        var sonuc = await TakasAsync(slot, derslik);

        OnaylandiMi(sonuc);
        Assert.Equal(new[] { (s1z1, s1d1, T), (s1z2, s1d2, L) }, await ProgramAsync(S1));
        Assert.Equal(new[] { (s2z1, s2d1, T), (s2z2, s2d2, T) }, await ProgramAsync(S2));
    }

    // S1'in iki bloğu aynı gün (Pzt 09@D1, Pzt 10@D2), satır kimlikleri saat sırasının tersi; yalnız zaman dilimi takası. Eşleme saat sırasıyla.
    [Fact]
    public async Task D35b_AyniGunIkiBlok_EslemeSaatSirasiyla()
    {
        Kur(tersSube: S1, s1: [(Z1, D1, T), (Z2, D2, T)], s2: [(Z3, D3, T), (Z4, D4, T)]);

        var sonuc = await TakasAsync(slot: true, derslik: false);

        OnaylandiMi(sonuc);
        Assert.Equal(new[] { (Z3, D1, T), (Z4, D2, T) }, await ProgramAsync(S1));
        Assert.Equal(new[] { (Z1, D3, T), (Z2, D4, T) }, await ProgramAsync(S2));
    }

    // Blok sayıları farklı tam takas: S1 iki blok (Theory, Lab; ders 3 saat, eksik atama serbest), S2 üç blok (Pzt 09@D6, Pzt 10, Sal 10;
    // hocalar iki gün ders verir). S1'in türleri sırayla kendi bloklarından, fazladan satır varsayılan türde.
    [Fact]
    public async Task D35b_FarkliBlokSayisi_TamTakas_TurSirasiKorunur()
    {
        Kur(s1Uygulamali: true, s1Saat: (2, 1), s2: [(Z1, D6, T), (Z2, D2, T), (Z4, D4, T)]);

        var sonuc = await TakasAsync(slot: true, derslik: true);

        OnaylandiMi(sonuc);
        Assert.Equal(new[] { (Z1, D6, T), (Z2, D2, L), (Z4, D4, T) }, await ProgramAsync(S1));
        Assert.Equal(new[] { (Z1, D1, T), (Z3, D3, T) }, await ProgramAsync(S2));
    }

    // 200 beklenir; değilse çakışmalar hata iletisinde görünür.
    private static void OnaylandiMi(IActionResult sonuc)
    {
        if (sonuc is ConflictObjectResult c)
        {
            var cakismalar = (IEnumerable<SchedulingConflictDto>)c.Value!.GetType().GetProperty("conflicts")!.GetValue(c.Value)!;
            Assert.Fail("409: " + string.Join("; ", cakismalar.Select(k => $"{k.SectionId}/{k.RelatedSectionId} {k.ConflictType} {k.Message}")));
        }
        Assert.IsType<OkObjectResult>(sonuc);
    }

    // tersSube: o şubenin satırları ters sırayla (geç satır önce, küçük kimlikle) eklenir. ucuncu: tek satırlı S3 (zaman dilimi, derslik).
    // s1, s2: varsayılan yerine şubenin satırları (kronolojik); S2'nin teori saati satır sayısıdır. s1Saat: S1 dersinin (teori, uygulama) saati.
    private void Kur(bool s1Uygulamali = false, int? tersSube = null, (int Dilim, int Derslik)? ucuncu = null,
        (int, int, ScheduleBlockType)[]? s1 = null, (int, int, ScheduleBlockType)[]? s2 = null, (int, int)? s1Saat = null)
    {
        _db.Faculties.Add(new Faculty { Id = 1, Name = "Deneme Fakültesi", Code = "DF" });
        _db.Departments.Add(new Department { Id = 3, FacultyId = 1, Name = "Deneme Bölümü", Code = "DB" });
        _db.AcademicTerms.Add(new AcademicTerm { Id = 5, Name = "2031 Güz", ShortEnglishName = "Fall 2031", TermType = TermType.Fall, Year = 2031 });
        _db.Classrooms.AddRange(Derslik(D1), Derslik(D2), Derslik(D3), Derslik(D4), Derslik(D5), Derslik(D6), Derslik(D7));
        _db.TimeSlots.AddRange(
            Dilim(Z1, DayOfWeek.Monday, 9, 1), Dilim(Z2, DayOfWeek.Monday, 10, 2),
            Dilim(Z3, DayOfWeek.Tuesday, 9, 1), Dilim(Z4, DayOfWeek.Tuesday, 10, 2), Dilim(Z5, DayOfWeek.Wednesday, 9, 1));

        SubeEkle(S1, D5, s1Saat ?? (s1Uygulamali ? (1, 1) : (2, 0)), 61, s1 ?? [(Z1, D1, T), (Z3, D3, s1Uygulamali ? L : T)], tersSube == S1);
        SubeEkle(S2, D6, (s2?.Length ?? 2, 0), 71, s2 ?? [(Z2, D2, T), (Z4, D4, T)], tersSube == S2);
        if (ucuncu is { } u)
            SubeEkle(S3, u.Derslik, (1, 0), 81, [(u.Dilim, u.Derslik, T)], ters: false);
        SubeEkle(S4, D7, (1, 0), 91, [(Z5, D7, T)], ters: false);
        _db.SaveChanges();
    }

    // Satırlar kronolojik verilir; ters ise kimlik ve ekleme sırası tersine çevrilir.
    private void SubeEkle(int sube, int anaDerslik, (int Teori, int Uygulama) saat, int ilkKimlik,
        (int Dilim, int Derslik, ScheduleBlockType Tur)[] satirlar, bool ters)
    {
        _db.Instructors.Add(new Instructor { Id = sube + 20, UserId = sube + 20, DepartmentId = 3, Title = "Dr.", MaxWeeklyHours = 20 });
        _db.Courses.Add(new Course
        {
            Id = sube + 10, Code = $"DPR{sube}", Name = "Deneme Dersi", Ects = 5, TheoryHours = saat.Teori, LabHours = saat.Uygulama, DepartmentId = 3
        });
        _db.CourseSections.Add(new CourseSection
        {
            Id = sube, CourseId = sube + 10, TermId = 5, SectionNumber = 1, Capacity = 30, ClassroomId = anaDerslik, InstructorId = sube + 20,
            Status = CourseSectionStatus.Active
        });
        var sirali = ters ? satirlar.Reverse().ToArray() : satirlar;
        for (var i = 0; i < sirali.Length; i++)
        {
            var (dilim, derslik, tur) = sirali[i];
            _db.SectionSchedules.Add(new SectionSchedule
            {
                Id = ilkKimlik + i, CourseSectionId = sube, ClassroomId = derslik, TimeSlotId = dilim, BlockType = tur
            });
        }
    }

    private async Task<IActionResult> TakasAsync(bool slot, bool derslik)
    {
        var servis = new SchedulingService(_db, new ConstraintStateLoader(_db), new ScheduleWriter(_db),
            SIS.Tests.Scheduling.Rules.TestRuleHelpers.BuildEngine());
        // Takas akademik işlemler servisini kullanmıyor.
        var controller = new SchedulingController(servis, null!, _db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "SystemAdministrator")], "Test"))
                }
            }
        };
        return await controller.SwapSections(
            new SwapSectionsRequestDto { SectionId1 = S1, SectionId2 = S2, SwapSlots = slot, SwapClassrooms = derslik }, CancellationToken.None);
    }

    // Şubenin kalıcı programı (zaman dilimi, derslik, blok türü), zaman dilimi kimliğiyle (= kronolojik) sıralı; ayrı bir context'le.
    private async Task<(int, int, ScheduleBlockType)[]> ProgramAsync(int sube)
    {
        await using var db = YeniContext();
        var satirlar = await db.SectionSchedules.Where(s => s.CourseSectionId == sube)
            .Select(s => new { s.TimeSlotId, s.ClassroomId, s.BlockType }).ToListAsync();
        return satirlar.OrderBy(s => s.TimeSlotId).Select(s => (s.TimeSlotId, s.ClassroomId!.Value, s.BlockType)).ToArray();
    }

    // Şubelerin ana dersliği (CourseSection.ClassroomId), ayrı bir context'le.
    private async Task<(int, int)[]> AnaDersliklerAsync()
    {
        await using var db = YeniContext();
        var subeler = await db.CourseSections.Select(s => new { s.Id, s.ClassroomId }).ToListAsync();
        return subeler.OrderBy(s => s.Id).Select(s => (s.Id, s.ClassroomId!.Value)).ToArray();
    }

    private AppDbContext YeniContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_vt).Options);

    private static Classroom Derslik(int id) => new() { Id = id, Name = $"DP-{id}", BuildingName = "DP", Capacity = 40, Type = ClassroomType.Lecture };

    private static TimeSlot Dilim(int id, DayOfWeek gun, int saat, int sira) => new()
    {
        Id = id, TermId = 5, DayOfWeek = gun, StartTime = new TimeOnly(saat, 0), EndTime = new TimeOnly(saat, 50),
        SlotOrder = sira, DisplayName = $"{gun} {sira}"
    };
}
