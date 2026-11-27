using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Tests.Postgres.DersProgrami;

// D35: onay yokken tam takas (zaman dilimi + derslik) gerçek uçtan kabul edilir ve kalıcı programa yazılır. Aynı kaydetmede iki satır
// silinir ve aynı (derslik, zaman dilimi) çiftleri ters şubelerle yeniden eklenir; derslik-zaman tekil dizini altında gerçek PostgreSQL.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class D35_TamTakasTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task D35_TamTakas_200_KaliciProgramVeAnaDerslikTakasEdilir()
    {
        var veri = await DersProgramiVeriKurucu.KurAsync(fixture);
        var s1 = await veri.SubeEkleAsync(1);
        var s2 = await veri.SubeEkleAsync(2);
        await YerlestirAsync(s1, veri.D1Id, veri.Z1Id);
        await YerlestirAsync(s2, veri.D2Id, veri.Z2Id);
        using var istemci = await DersProgramiVeriKurucu.YoneticiIstemcisiAsync(uygulama.Al(fixture.BaglantiCumlesi));

        var yanit = await istemci.PostAsJsonAsync("/api/scheduling/swap",
            new { sectionId1 = s1, sectionId2 = s2, swapSlots = true, swapClassrooms = true });

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal(new[] { ((int?)veri.D2Id, veri.Z2Id) }, await veri.ProgramAsync(s1));
        Assert.Equal(new[] { ((int?)veri.D1Id, veri.Z1Id) }, await veri.ProgramAsync(s2));
        await using var db = fixture.YeniContext();
        Assert.Equal(new int?[] { veri.D2Id, veri.D1Id },
            await db.CourseSections.Where(s => s.Id == s1 || s.Id == s2).OrderBy(s => s.SectionNumber).Select(s => s.ClassroomId).ToArrayAsync());
    }

    private async Task YerlestirAsync(int sube, int derslik, int dilim)
    {
        await using var db = fixture.YeniContext();
        await db.CourseSections.Where(s => s.Id == sube).ExecuteUpdateAsync(s => s.SetProperty(k => k.ClassroomId, derslik));
        db.SectionSchedules.Add(new SectionSchedule { CourseSectionId = sube, ClassroomId = derslik, TimeSlotId = dilim });
        await db.SaveChangesAsync();
    }
}
