using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SIS.Application.FacultyPlanning.DTOs;
using SIS.Application.FacultyPlanning.Services;
using SIS.Application.Scheduling.DTOs;

namespace SIS.Tests.Postgres.DersProgrami;

// E-03: çakışma denetimi atlanınca (SkipHardValidation) ya da onaylanınca (Force) ikinci yazımı yalnız veritabanı dizini durdurur.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class E03_DerslikZamanTekilDizinTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Dizin = "IX_SectionSchedules_Classroom_TimeSlot_Unique";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task E03_IkinciSubeAyniDerslikAyniZaman_TekilDizinReddeder_IlkAtamaKalir(bool atla, bool zorla)
    {
        var veri = await DersProgramiVeriKurucu.KurAsync(fixture);
        var a = await veri.SubeEkleAsync(1);
        var b = await veri.SubeEkleAsync(2);
        await AtaAsync(veri, a, veri.D1Id, DersProgramiVeriKurucu.Z1, hoca: null, atla, zorla);

        var hata = await Assert.ThrowsAsync<DbUpdateException>(
            () => AtaAsync(veri, b, veri.D1Id, DersProgramiVeriKurucu.Z1, hoca: null, atla, zorla));
        var pg = Assert.IsType<PostgresException>(hata.InnerException);
        Assert.Equal("23505", pg.SqlState);
        Assert.Equal(Dizin, pg.ConstraintName);
        Assert.Equal(new[] { ((int?)veri.D1Id, veri.Z1Id) }, await veri.ProgramAsync(a));
        Assert.Empty(await veri.ProgramAsync(b));
        Assert.Equal((1, 0), (await AtamaKaydiAsync(a), await AtamaKaydiAsync(b)));

        // Karşı örnek: aynı derslik, başka zaman dilimi kabul edilir ve atama kaydı oluşur.
        await AtaAsync(veri, b, veri.D1Id, DersProgramiVeriKurucu.Z2, hoca: null, atla, zorla);
        Assert.Equal(new[] { ((int?)veri.D1Id, veri.Z2Id) }, await veri.ProgramAsync(b));
        Assert.Equal((1, 1), (await AtamaKaydiAsync(a), await AtamaKaydiAsync(b)));
    }

    // Aynı bayraklarla (SkipHardValidation ya da Force) aynı hoca aynı zamanda iki dersliğe yazılır: veritabanında karşılığı yok.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task E03_AyniHocaAyniZamanFarkliDerslik_VeritabaniKabulEder(bool atla, bool zorla)
    {
        var veri = await DersProgramiVeriKurucu.KurAsync(fixture);
        var a = await veri.SubeEkleAsync(1);
        var b = await veri.SubeEkleAsync(2);
        var c = await veri.SubeEkleAsync(3);
        await AtaAsync(veri, a, veri.D1Id, DersProgramiVeriKurucu.Z1, veri.HocaId, atla, zorla);
        await AtaAsync(veri, b, veri.D2Id, DersProgramiVeriKurucu.Z1, veri.HocaId, atla, zorla);

        Assert.Equal(new[] { ((int?)veri.D1Id, veri.Z1Id) }, await veri.ProgramAsync(a));
        Assert.Equal(new[] { ((int?)veri.D2Id, veri.Z1Id) }, await veri.ProgramAsync(b));
        Assert.Equal(new[] { a, b }, await HocaninSubeleriAsync(veri));

        // Karşı örnek: hocasız bir şube aynı derslik ve zamana yazılınca dizine takılır.
        var hata = await Assert.ThrowsAsync<DbUpdateException>(
            () => AtaAsync(veri, c, veri.D2Id, DersProgramiVeriKurucu.Z1, hoca: null, atla, zorla));
        Assert.Equal(Dizin, Assert.IsType<PostgresException>(hata.InnerException).ConstraintName);
        Assert.Empty(await veri.ProgramAsync(c));
    }

    // Gerçek atama servisi, her çağrıda uygulamanın yeni bir DI kapsamından.
    private async Task AtaAsync(
        DersProgramiVeriKurucu veri, int sube, int derslik, (TimeOnly Bas, TimeOnly Bit) z, int? hoca, bool atla, bool zorla)
    {
        using var kapsam = uygulama.Al(fixture.BaglantiCumlesi).Services.CreateScope();
        await kapsam.ServiceProvider.GetRequiredService<IFacultyPlanningService>().AssignSectionAsync(new AssignSectionRequest
        {
            SectionId = sube, ClassroomId = derslik, InstructorId = hoca, SkipHardValidation = atla, Force = zorla,
            ScheduleSlots = [new ScheduleSlotDto { Day = (int)DayOfWeek.Monday, StartTime = z.Bas, EndTime = z.Bit }]
        }, veri.KullaniciId.ToString());
    }

    private async Task<int> AtamaKaydiAsync(int sube)
    {
        await using var db = fixture.YeniContext();
        return await db.SchedulingAuditLogs.CountAsync(l => l.SectionId == sube && l.Action == "Assign");
    }

    private async Task<int[]> HocaninSubeleriAsync(DersProgramiVeriKurucu veri)
    {
        await using var db = fixture.YeniContext();
        return await db.SectionSchedules.Where(s => s.CourseSection.InstructorId == veri.HocaId && s.TimeSlotId == veri.Z1Id)
            .Select(s => s.CourseSectionId).OrderBy(i => i).ToArrayAsync();
    }
}
