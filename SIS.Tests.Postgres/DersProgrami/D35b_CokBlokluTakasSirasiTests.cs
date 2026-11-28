using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Tests.Postgres.DersProgrami;

// D35b: çok bloklu yalnız zaman dilimi takasında satırlar kronolojik (gün, başlangıç saati) eşlenir; gerçek PostgreSQL, gerçek uç.
// S1'de bütün sıra kaynakları kronolojik, S2'de hepsi ters kurulur: zaman dilimi kimliği, derslik kimliği ve satırın tablodaki fiziksel
// yeri (zararsız güncelleme satırın yeni sürümünü sona taşır). Yalnız satır kimliği iki şubede de kronolojik.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class D35b_CokBlokluTakasSirasiTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task D35b_ZararsizGuncellemeSonrasi_YalnizZamanDilimiTakasi_KronolojikEslenir()
    {
        var veri = await DersProgramiVeriKurucu.KurAsync(fixture);
        // Salı zaman dilimleri kronolojik olmayan sırayla oluşturulur: kimlik sırası 14, 09, 10, 13.
        var z14 = await veri.DilimEkleAsync(DayOfWeek.Tuesday, new TimeOnly(14, 0), 4);
        var z09 = await veri.DilimEkleAsync(DayOfWeek.Tuesday, new TimeOnly(9, 0), 1);
        var z10 = await veri.DilimEkleAsync(DayOfWeek.Tuesday, new TimeOnly(10, 0), 2);
        var z13 = await veri.DilimEkleAsync(DayOfWeek.Tuesday, new TimeOnly(13, 0), 3);
        var d3 = await veri.DerslikEkleAsync("DP-103");
        var d4 = await veri.DerslikEkleAsync("DP-104");
        var ders = await veri.DersEkleAsync("DPR201", teoriSaati: 2);
        var s1 = await veri.SubeEkleAsync(1, ders);
        var s2 = await veri.SubeEkleAsync(2, ders);
        await YerlestirAsync(s1, veri.D1Id, (veri.D1Id, z09), (d3, z13));
        await YerlestirAsync(s2, d4, (d4, z10), (veri.D2Id, z14));
        await using (var db = fixture.YeniContext())
            Assert.Equal(1, await db.SectionSchedules.Where(s => s.CourseSectionId == s2 && s.TimeSlotId == z10)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.BlockType, k => k.BlockType)));
        using var istemci = await DersProgramiVeriKurucu.YoneticiIstemcisiAsync(uygulama.Al(fixture.BaglantiCumlesi));

        var yanit = await istemci.PostAsJsonAsync("/api/scheduling/swap",
            new { sectionId1 = s1, sectionId2 = s2, swapSlots = true, swapClassrooms = false });

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal(new[] { (veri.D1Id, z10), (d3, z14) }, await KronolojikProgramAsync(s1));
        Assert.Equal(new[] { (d4, z09), (veri.D2Id, z13) }, await KronolojikProgramAsync(s2));
    }

    // Satırlar verilen sırayla, ayrı kaydetmelerle eklenir (satır kimlikleri bu sırayı izler).
    private async Task YerlestirAsync(int sube, int anaDerslik, params (int Derslik, int Dilim)[] satirlar)
    {
        await using var db = fixture.YeniContext();
        await db.CourseSections.Where(s => s.Id == sube).ExecuteUpdateAsync(s => s.SetProperty(k => k.ClassroomId, anaDerslik));
        foreach (var (derslik, dilim) in satirlar)
        {
            db.SectionSchedules.Add(new SectionSchedule { CourseSectionId = sube, ClassroomId = derslik, TimeSlotId = dilim });
            await db.SaveChangesAsync();
        }
    }

    // Şubenin program satırları (derslik, zaman dilimi), zaman dilimi başlangıç saatine göre (hepsi Salı).
    private async Task<(int, int)[]> KronolojikProgramAsync(int sube)
    {
        await using var db = fixture.YeniContext();
        var satirlar = await db.SectionSchedules.Where(s => s.CourseSectionId == sube)
            .OrderBy(s => s.TimeSlot.StartTime).Select(s => new { s.ClassroomId, s.TimeSlotId }).ToListAsync();
        return satirlar.Select(s => (s.ClassroomId!.Value, s.TimeSlotId)).ToArray();
    }
}
