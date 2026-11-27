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

// D35: takas doğrulaması her şubeyi ortağının takas sonrası konumuyla denetler (ortağın eski konumuyla sahte çakışma yok) ve
// doğrulanan son durum yazılan son durumdur. Onay yok; gerçek controller ve gerçek SchedulingService (gerçek kural motoru).
// Kimlikler varlık türleri arasında ayrıktır: derslik 11-12, zaman dilimi 21-22, şube 31-33, ders 41-43, hoca 51-53.
public sealed class D35_TakasDogrulamaTests : IDisposable
{
    private const int D1 = 11, D2 = 12, Z1 = 21, Z2 = 22, S1 = 31, S2 = 32, S3 = 33, Hoca1 = 51, Hoca2 = 52, Hoca3 = 53;

    private readonly string _vt = Guid.NewGuid().ToString();
    private readonly AppDbContext _db;

    public D35_TakasDogrulamaTests() => _db = YeniContext();

    public void Dispose() => _db.Dispose();

    // Üç kip; iki şubenin hocası ayrı ya da aynı (takas sonrası saatler çakışmaz). Yalnız zaman dilimi takası dersliklere dokunmaz.
    // Her kipte şubenin ana dersliği kendi program satırlarındaki derslikle aynı kalır.
    [Theory]
    [InlineData(true, false, false, D1, Z2, D2, Z1)]
    [InlineData(true, false, true, D1, Z2, D2, Z1)]
    [InlineData(true, true, false, D2, Z2, D1, Z1)]
    [InlineData(true, true, true, D2, Z2, D1, Z1)]
    [InlineData(false, true, false, D2, Z1, D1, Z2)]
    [InlineData(false, true, true, D2, Z1, D1, Z2)]
    public async Task D35_OnayYok_Takas200_DogrulananProgramYazilir(
        bool slot, bool derslik, bool ayniHoca, int s1Derslik, int s1Dilim, int s2Derslik, int s2Dilim)
    {
        Kur(ayniHoca);

        var sonuc = await TakasAsync(slot, derslik);

        Assert.IsType<OkObjectResult>(sonuc);
        Assert.Equal(new[] { (S1, s1Derslik, s1Dilim), (S2, s2Derslik, s2Dilim) }, await ProgramAsync());
        Assert.Equal(new[] { (S1, s1Derslik), (S2, s2Derslik) }, await AnaDersliklerAsync());
    }

    // Gerçek çakışma korunur: üçüncü şube S1'in yeni konumunda. Yalnız derslik takasında S1 yeni dersliğiyle denetlenir.
    [Theory]
    [InlineData(true, false, D1, Z2)]
    [InlineData(false, true, D2, Z1)]
    public async Task D35_UcuncuSubeYeniKonumda_Takas409_ProgramDegismez(bool slot, bool derslik, int s3Derslik, int s3Dilim)
    {
        Kur(ayniHoca: false, ucuncu: (s3Derslik, s3Dilim));

        var sonuc = await TakasAsync(slot, derslik);

        var cakisma = Assert.IsType<ConflictObjectResult>(sonuc);
        Assert.Equal("Takas sonrasinda cakisma tespit edildi. Takas iptal edildi.",
            (string?)cakisma.Value!.GetType().GetProperty("error")!.GetValue(cakisma.Value));
        var cakismalar = (IEnumerable<SchedulingConflictDto>)cakisma.Value.GetType().GetProperty("conflicts")!.GetValue(cakisma.Value)!;
        Assert.Contains(cakismalar, c => c.SectionId == S1 && c.RelatedSectionId == S3);
        Assert.DoesNotContain(cakismalar, c => (c.SectionId, c.RelatedSectionId) is (S1, S2) or (S2, S1));
        Assert.Equal(new[] { (S1, D1, Z1), (S2, D2, Z2), (S3, s3Derslik, s3Dilim) }, await ProgramAsync());
        Assert.Equal(new[] { (S1, D1), (S2, D2), (S3, s3Derslik) }, await AnaDersliklerAsync());
    }

    // S1 (D1, Z1), S2 (D2, Z2); isteğe bağlı üçüncü şube (üçüncü hoca). Her şubenin ayrı dersi, tek program satırı.
    private void Kur(bool ayniHoca, (int Derslik, int Dilim)? ucuncu = null)
    {
        _db.Faculties.Add(new Faculty { Id = 1, Name = "Deneme Fakültesi", Code = "DF" });
        _db.Departments.Add(new Department { Id = 3, FacultyId = 1, Name = "Deneme Bölümü", Code = "DB" });
        _db.AcademicTerms.Add(new AcademicTerm { Id = 5, Name = "2031 Güz", ShortEnglishName = "Fall 2031", TermType = TermType.Fall, Year = 2031 });
        _db.Classrooms.AddRange(Derslik(D1), Derslik(D2));
        _db.TimeSlots.AddRange(Dilim(Z1, 9, 1), Dilim(Z2, 10, 2));
        _db.Instructors.AddRange(Hoca(Hoca1), Hoca(Hoca2), Hoca(Hoca3));
        var subeler = new List<(int, int, int, int)> { (S1, D1, Z1, Hoca1), (S2, D2, Z2, ayniHoca ? Hoca1 : Hoca2) };
        if (ucuncu is { } u)
            subeler.Add((S3, u.Derslik, u.Dilim, Hoca3));
        foreach (var (sube, derslik, dilim, hoca) in subeler)
        {
            _db.Courses.Add(new Course { Id = sube + 10, Code = $"DPR{sube}", Name = "Deneme Dersi", Ects = 5, TheoryHours = 1, DepartmentId = 3 });
            _db.CourseSections.Add(new CourseSection
            {
                Id = sube, CourseId = sube + 10, TermId = 5, SectionNumber = 1, Capacity = 30, ClassroomId = derslik, InstructorId = hoca,
                Status = CourseSectionStatus.Active
            });
            _db.SectionSchedules.Add(new SectionSchedule { CourseSectionId = sube, ClassroomId = derslik, TimeSlotId = dilim });
        }
        _db.SaveChanges();
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

    // Kalıcı program (şube, derslik, zaman dilimi), ayrı bir context'le aynı veritabanından.
    private async Task<(int, int, int)[]> ProgramAsync()
    {
        await using var db = YeniContext();
        var satirlar = await db.SectionSchedules.OrderBy(s => s.CourseSectionId)
            .Select(s => new { s.CourseSectionId, s.ClassroomId, s.TimeSlotId }).ToListAsync();
        return satirlar.Select(s => (s.CourseSectionId, s.ClassroomId!.Value, s.TimeSlotId)).ToArray();
    }

    // Şubelerin ana dersliği (CourseSection.ClassroomId), ayrı bir context'le.
    private async Task<(int, int)[]> AnaDersliklerAsync()
    {
        await using var db = YeniContext();
        var subeler = await db.CourseSections.OrderBy(s => s.Id).Select(s => new { s.Id, s.ClassroomId }).ToListAsync();
        return subeler.Select(s => (s.Id, s.ClassroomId!.Value)).ToArray();
    }

    private AppDbContext YeniContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_vt).Options);

    private static Classroom Derslik(int id) => new() { Id = id, Name = $"DP-{id}", BuildingName = "DP", Capacity = 40, Type = ClassroomType.Lecture };

    private static Instructor Hoca(int id) => new() { Id = id, UserId = id, DepartmentId = 3, Title = "Dr.", MaxWeeklyHours = 20 };

    private static TimeSlot Dilim(int id, int saat, int sira) => new()
    {
        Id = id, TermId = 5, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(saat, 0), EndTime = new TimeOnly(saat, 50),
        SlotOrder = sira, DisplayName = $"Pazartesi {sira}"
    };
}
