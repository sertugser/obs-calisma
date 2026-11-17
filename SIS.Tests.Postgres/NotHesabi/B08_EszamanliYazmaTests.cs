using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Domain.Enums;

namespace SIS.Tests.Postgres.NotHesabi;

// B-08: aynı not ve paylaşılan bileşen üzerine ayrı context'lerden sıralı yazmalar.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class B08_EszamanliYazmaTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Bilinen hata F-06: not tablolarında eşzamanlı yazma denetimi yok.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task B08_AyniPuanaIkiYazma_SonYazanKazanir_EskiContextHatasiz()
    {
        var veri = await NotVeriKurucu.KurAsync(fixture);
        var subeId = await veri.SubeEkleAsync(hoca: 0, no: 1);
        var vize = await veri.BilesenEkleAsync("Vize", 40m, GradeComponentType.Midterm, 1);
        var (ogrenciId, kayitId) = await veri.OgrenciEkleAsync(subeId, "01");

        var mesajlar = new List<string>();
        foreach (var puan in new[] { 70m, 90m })
            mesajlar.Add(await PuanGonderAsync(veri, subeId, vize, ogrenciId, puan));
        Assert.Equal(new[] { "1 not eklendi, 0 not guncellendi.", "0 not eklendi, 1 not guncellendi." }, mesajlar);
        Assert.Equal(90m, await PuanAsync(kayitId));

        // Puanı önceden okumuş context, araya giren yazmanın üstüne hatasız yazıyor.
        await using var eski = fixture.YeniContext();
        var eskiNot = await eski.StudentGrades.SingleAsync(g => g.CourseRegistrationId == kayitId);
        await PuanGonderAsync(veri, subeId, vize, ogrenciId, 50m);
        Assert.Equal(50m, await PuanAsync(kayitId));
        eskiNot.Score = 60m;
        await eski.SaveChangesAsync();
        Assert.Equal(60m, await PuanAsync(kayitId));
    }

    // Kontrol: aynı desen xmin taşıyan şube tablosunda eşzamanlılık hatası veriyor.
    [Fact]
    public async Task B08_Kontrol_XminTasiyanSubede_EskiContextReddedilir()
    {
        var veri = await NotVeriKurucu.KurAsync(fixture);
        var subeId = await veri.SubeEkleAsync(hoca: 0, no: 1);

        await using var eski = fixture.YeniContext();
        var eskiSube = await eski.CourseSections.SingleAsync(s => s.Id == subeId);
        await using (var db = fixture.YeniContext())
        {
            (await db.CourseSections.SingleAsync(s => s.Id == subeId)).Capacity = 40;
            await db.SaveChangesAsync();
        }
        eskiSube.Capacity = 50;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => eski.SaveChangesAsync());
        await using var son = fixture.YeniContext();
        Assert.Equal(40, (await son.CourseSections.SingleAsync(s => s.Id == subeId)).Capacity);
    }

    // Bilinen hata F-06: paylaşılan bileşen, sonuçlandırılmış şube varken başka şube üzerinden değişebiliyor.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task B08_SonuclandirilmisSubeVarken_KoordinatorBaskaSubedenAgirlikDegistirir()
    {
        var (veri, subeA, subeB, vize, final, kayitA, kayitB) = await IkiSubeKurAsync(koordinatorA: false);

        foreach (var (id, ad, agirlik, tur, sira) in new[] { (vize, "Vize", 30m, GradeComponentType.Midterm, 1), (final, "Final", 70m, GradeComponentType.Final, 2) })
        {
            await using var db = fixture.YeniContext();
            var dto = new GradeController.ComponentDto { Id = id, Name = ad, Weight = agirlik, Type = tur, Order = sira };
            Assert.IsType<OkObjectResult>(await veri.Controller(db, hoca: 1).UpsertComponent(subeB, dto));
        }

        await using (var db = fixture.YeniContext())
            Assert.IsType<OkObjectResult>(await veri.Controller(db, hoca: 1).Finalize(subeB));

        await using var son = fixture.YeniContext();
        Assert.Equal(new[] { 30m, 70m }, await son.GradeComponents.OrderBy(b => b.Order).Select(b => b.Weight).ToListAsync());
        Assert.True((await son.CourseSections.SingleAsync(s => s.Id == subeA)).IsFinalized);
        Assert.Equal(GradeCode.CB, (await son.CourseRegistrations.SingleAsync(r => r.Id == kayitA)).Grade); // 40/60 ile 64
        Assert.Equal(GradeCode.CC, (await son.CourseRegistrations.SingleAsync(r => r.Id == kayitB)).Grade); // aynı puan, 30/70 ile 58
    }

    // Karşı örnek: koordinatör şube sonuçlandırılmışsa aynı değişiklik o şube üzerinden reddediliyor.
    [Fact]
    public async Task B08_KarsiOrnek_SonuclandirilmisKoordinatorSubeden_Reddedilir()
    {
        var (veri, subeA, _, vize, _, _, _) = await IkiSubeKurAsync(koordinatorA: true);

        await using (var db = fixture.YeniContext())
        {
            var dto = new GradeController.ComponentDto { Id = vize, Name = "Vize", Weight = 30m, Type = GradeComponentType.Midterm, Order = 1 };
            var sonuc = Assert.IsType<BadRequestObjectResult>(await veri.Controller(db, hoca: 0).UpsertComponent(subeA, dto));
            Assert.StartsWith("Sonuclandirilmis ders icin bilesen degistirilemez.", NotVeriKurucu.Oku<string>(sonuc.Value, "error"));
        }

        await using var son = fixture.YeniContext();
        Assert.Equal(new[] { 40m, 60m }, await son.GradeComponents.OrderBy(b => b.Order).Select(b => b.Weight).ToListAsync());
    }

    // İki hoca, iki şube, ortak bileşenler (vize 40 / final 60); her şubede 100/40 alan bir öğrenci; A şubesi sonuçlandırılmış.
    private async Task<(NotVeriKurucu, int SubeA, int SubeB, int Vize, int Final, int KayitA, int KayitB)> IkiSubeKurAsync(bool koordinatorA)
    {
        var veri = await NotVeriKurucu.KurAsync(fixture);
        var subeA = await veri.SubeEkleAsync(hoca: 0, no: 1, koordinator: koordinatorA);
        var subeB = await veri.SubeEkleAsync(hoca: 1, no: 2, koordinator: !koordinatorA);
        var vize = await veri.BilesenEkleAsync("Vize", 40m, GradeComponentType.Midterm, 1);
        var final = await veri.BilesenEkleAsync("Final", 60m, GradeComponentType.Final, 2);
        await veri.HarfOlcutuEkleAsync();
        var kayitlar = new List<int>();
        foreach (var (sube, no) in new[] { (subeA, "01"), (subeB, "02") })
        {
            var (_, kayitId) = await veri.OgrenciEkleAsync(sube, no);
            await veri.PuanEkleAsync(kayitId, vize, 100m);
            await veri.PuanEkleAsync(kayitId, final, 40m);
            kayitlar.Add(kayitId);
        }

        await using (var db = fixture.YeniContext())
            Assert.IsType<OkObjectResult>(await veri.Controller(db, hoca: 0).Finalize(subeA));
        await using (var db = fixture.YeniContext())
            Assert.Equal(GradeCode.CB, (await db.CourseRegistrations.SingleAsync(r => r.Id == kayitlar[0])).Grade);
        return (veri, subeA, subeB, vize, final, kayitlar[0], kayitlar[1]);
    }

    private async Task<string> PuanGonderAsync(NotVeriKurucu veri, int subeId, int bilesenId, int ogrenciId, decimal puan)
    {
        await using var db = fixture.YeniContext();
        var sonuc = Assert.IsType<OkObjectResult>(await veri.Controller(db).SubmitGrades(subeId, new GradeController.GradeSubmitDto
        {
            ComponentId = bilesenId,
            Grades = [new GradeController.StudentScoreDto { StudentId = ogrenciId, Score = puan }]
        }));
        return NotVeriKurucu.Oku<string>(sonuc.Value, "message");
    }

    private async Task<decimal?> PuanAsync(int kayitId)
    {
        await using var db = fixture.YeniContext();
        return await db.StudentGrades.Where(g => g.CourseRegistrationId == kayitId).Select(g => g.Score).SingleAsync();
    }
}
