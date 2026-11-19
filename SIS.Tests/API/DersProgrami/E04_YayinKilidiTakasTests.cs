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

// E-04: fakülte programı o dönem için onaylıysa (yayında) iki şubenin derslik takası reddedilir.
// Gerçek controller ve gerçek SchedulingService (gerçek kural motoru); yalnız derslikler takas edilir.
// Kimlikler varlık türleri arasında ayrıktır: derslik 11-12, zaman dilimi 21-22, şube 31-32.
public sealed class E04_YayinKilidiTakasTests : IDisposable
{
    private const int Fakulte = 1, BaskaFakulte = 2, Donem = 5, BaskaDonem = 6;
    private const int D1 = 11, D2 = 12, Z1 = 21, Z2 = 22, S1 = 31, S2 = 32;

    private readonly string _vt = Guid.NewGuid().ToString();
    private readonly AppDbContext _db;

    public E04_YayinKilidiTakasTests() => _db = YeniContext();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task E04_FakulteProgramiOnayli_DerslikTakasi400_ProgramDegismez()
    {
        Kur(bolumlu: true, onayFakulte: Fakulte, onayDonem: Donem);

        var sonuc = await TakasAsync();

        var hata = Assert.IsType<BadRequestObjectResult>(sonuc);
        Assert.Equal("Bu fakültenin programı onaylanmış (yayında). Takas için önce Program Onayını geri alın.", Hata(hata.Value));
        Assert.Equal(new[] { (S1, D1, Z1), (S2, D2, Z2) }, await ProgramAsync());
    }

    // Karşı örnekler: onay yok, başka dönemde, başka fakültede ya da dersler ortak (bölümsüz).
    [Theory]
    [InlineData(true, null, null)]
    [InlineData(true, Fakulte, BaskaDonem)]
    [InlineData(true, BaskaFakulte, Donem)]
    [InlineData(false, Fakulte, Donem)]
    public async Task E04_OnayKosuluTutmuyor_DerslikTakasiUygulanir(bool bolumlu, int? onayFakulte, int? onayDonem)
    {
        Kur(bolumlu, onayFakulte, onayDonem);

        var sonuc = await TakasAsync();

        Assert.IsType<OkObjectResult>(sonuc);
        Assert.Equal(new[] { (S1, D2, Z1), (S2, D1, Z2) }, await ProgramAsync());
    }

    // İki fakülte (bölümleri 3 ve 4), iki dönem; Donem'de iki ders, her birinin tek şubesi ve tek program satırı.
    private void Kur(bool bolumlu, int? onayFakulte, int? onayDonem)
    {
        _db.Faculties.AddRange(new Faculty { Id = Fakulte, Name = "Deneme Fakültesi", Code = "DF" },
            new Faculty { Id = BaskaFakulte, Name = "Diğer Fakülte", Code = "DG" });
        _db.Departments.AddRange(new Department { Id = 3, FacultyId = Fakulte, Name = "Deneme Bölümü", Code = "DB" },
            new Department { Id = 4, FacultyId = BaskaFakulte, Name = "Diğer Bölüm", Code = "DGB" });
        _db.AcademicTerms.AddRange(Yil(Donem, 2031), Yil(BaskaDonem, 2032));
        _db.Classrooms.AddRange(Derslik(D1), Derslik(D2));
        _db.TimeSlots.AddRange(Dilim(Z1, 9, 1), Dilim(Z2, 10, 2));
        foreach (var (sube, derslik, dilim) in new[] { (S1, D1, Z1), (S2, D2, Z2) })
        {
            _db.Courses.Add(new Course
            {
                Id = sube + 10, Code = $"DPR{sube}", Name = "Deneme Dersi", Ects = 5, TheoryHours = 1, DepartmentId = bolumlu ? 3 : null
            });
            _db.CourseSections.Add(new CourseSection
            {
                Id = sube, CourseId = sube + 10, TermId = Donem, SectionNumber = 1, Capacity = 30, ClassroomId = derslik,
                Status = CourseSectionStatus.Active
            });
            _db.SectionSchedules.Add(new SectionSchedule { CourseSectionId = sube, ClassroomId = derslik, TimeSlotId = dilim });
        }
        if (onayFakulte is int f && onayDonem is int d)
            _db.FacultyScheduleApprovals.Add(new FacultyScheduleApproval { FacultyId = f, TermId = d, ApprovedByUserId = 1 });
        _db.SaveChanges();
    }

    private async Task<IActionResult> TakasAsync()
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
            new SwapSectionsRequestDto { SectionId1 = S1, SectionId2 = S2, SwapClassrooms = true }, CancellationToken.None);
    }

    // Kalıcı program (şube, derslik, zaman dilimi), ayrı bir context'le aynı veritabanından.
    private async Task<(int, int, int)[]> ProgramAsync()
    {
        await using var db = YeniContext();
        var satirlar = await db.SectionSchedules.OrderBy(s => s.CourseSectionId)
            .Select(s => new { s.CourseSectionId, s.ClassroomId, s.TimeSlotId }).ToListAsync();
        return satirlar.Select(s => (s.CourseSectionId, s.ClassroomId!.Value, s.TimeSlotId)).ToArray();
    }

    private AppDbContext YeniContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_vt).Options);

    // Gövdenin tek alanı "error".
    private static string? Hata(object? govde)
    {
        var alan = Assert.Single(govde!.GetType().GetProperties());
        Assert.Equal("error", alan.Name);
        return (string?)alan.GetValue(govde);
    }

    private static AcademicTerm Yil(int id, int yil) => new()
    {
        Id = id, Name = $"{yil} Güz", ShortEnglishName = $"Fall {yil}", TermType = TermType.Fall, Year = yil
    };

    private static Classroom Derslik(int id) => new() { Id = id, Name = $"DP-{id}", BuildingName = "DP", Capacity = 40, Type = ClassroomType.Lecture };

    private static TimeSlot Dilim(int id, int saat, int sira) => new()
    {
        Id = id, TermId = Donem, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(saat, 0), EndTime = new TimeOnly(saat, 50),
        SlotOrder = sira, DisplayName = $"Pazartesi {sira}"
    };
}
