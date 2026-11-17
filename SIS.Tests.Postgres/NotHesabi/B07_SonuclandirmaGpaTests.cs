using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Application.GpaCalculation.DTOs;
using SIS.Application.GpaCalculation.Services;
using SIS.Domain.Enums;

namespace SIS.Tests.Postgres.NotHesabi;

// B-07: üç öğrencili şube sonuçlandırılıyor; harfler kaydedildikten sonra öğrenci başına GPA yeniden hesaplanıyor.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class B07_SonuclandirmaGpaTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const decimal EskiOrtalama = 1.11m;

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Bilinen hata F-05: sonuçlandırma ve GPA güncellemesi tek işlem içinde değil.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task B07_IkinciGpaHesabiHata_HarflerKalici_YalnizIlkCagrilanGuncel()
    {
        var (veri, subeId) = await KurAsync();
        SayanGpaServisi gpa;
        await using (var db = fixture.YeniContext())
        {
            gpa = new SayanGpaServisi(NotVeriKurucu.GercekGpa(db), hataCagrisi: 2);
            await Assert.ThrowsAsync<InvalidOperationException>(() => veri.Controller(db, gpa: gpa).Finalize(subeId));
        }

        Assert.Equal(2, gpa.Cagrilar.Count);
        await using (var db = fixture.YeniContext())
        {
            Assert.True((await db.CourseSections.SingleAsync(s => s.Id == subeId)).IsFinalized);
            var kayitlar = await db.CourseRegistrations.Where(r => r.CourseSectionId == subeId).Include(r => r.Student).ToListAsync();
            Assert.Equal(3, kayitlar.Count);
            Assert.All(kayitlar, k => Assert.Equal(GradeCode.BB, k.Grade)); // 80 → BB
            Assert.All(kayitlar, k => Assert.Equal(k.StudentId == gpa.Cagrilar[0] ? 3.00m : EskiOrtalama, k.Student.Cgpa));
            Assert.All(kayitlar, k => Assert.Equal(k.StudentId == gpa.Cagrilar[0] ? 3.00m : EskiOrtalama, k.Student.Sgpa));
        }

        // Tekrar sonuçlandırma (GPA hatasız) reddediliyor; iki öğrencinin ortalaması eski kalıyor.
        await using (var db = fixture.YeniContext())
        {
            var sonuc = Assert.IsType<BadRequestObjectResult>(await veri.Controller(db).Finalize(subeId));
            Assert.Equal("Bu section zaten sonuclandirilmis.", NotVeriKurucu.Oku<string>(sonuc.Value, "error"));
            Assert.Equal(2, await db.Students.CountAsync(s => s.Cgpa == EskiOrtalama));
        }
    }

    // Karşı örnek: GPA servisi hata vermezse üç öğrencinin de ortalaması güncelleniyor.
    [Fact]
    public async Task B07_KarsiOrnek_GpaHatasiz_UcOgrenciGuncel()
    {
        var (veri, subeId) = await KurAsync();
        SayanGpaServisi gpa;
        await using (var db = fixture.YeniContext())
        {
            gpa = new SayanGpaServisi(NotVeriKurucu.GercekGpa(db), hataCagrisi: null);
            Assert.IsType<OkObjectResult>(await veri.Controller(db, gpa: gpa).Finalize(subeId));
        }

        Assert.Equal(3, gpa.Cagrilar.Distinct().Count());
        await using (var db = fixture.YeniContext())
        {
            Assert.All(await db.Students.Where(s => gpa.Cagrilar.Contains(s.Id)).ToListAsync(), s => Assert.Equal(3.00m, s.Cgpa));
        }
    }

    private async Task<(NotVeriKurucu Veri, int SubeId)> KurAsync()
    {
        var veri = await NotVeriKurucu.KurAsync(fixture);
        var subeId = await veri.SubeEkleAsync(hoca: 0, no: 1);
        var vize = await veri.BilesenEkleAsync("Vize", 40m, GradeComponentType.Midterm, 1);
        var final = await veri.BilesenEkleAsync("Final", 60m, GradeComponentType.Final, 2);
        await veri.HarfOlcutuEkleAsync();
        foreach (var no in new[] { "01", "02", "03" })
        {
            var (_, kayitId) = await veri.OgrenciEkleAsync(subeId, no, EskiOrtalama);
            await veri.PuanEkleAsync(kayitId, vize, 80m);
            await veri.PuanEkleAsync(kayitId, final, 80m);
        }
        return (veri, subeId);
    }

    // Gerçek GPA servisini sarar; çağrı sırasını kaydeder, istenirse verilen sıradaki çağrıda hata fırlatır.
    private sealed class SayanGpaServisi(IGpaCalculationService ic, int? hataCagrisi) : IGpaCalculationService
    {
        public List<int> Cagrilar { get; } = [];

        public Task<GpaResultDto> RecalculateAndUpdateAsync(int studentId)
        {
            Cagrilar.Add(studentId);
            return Cagrilar.Count == hataCagrisi
                ? throw new InvalidOperationException("Deneme: GPA hesabı başarısız.")
                : ic.RecalculateAndUpdateAsync(studentId);
        }

        public Task<decimal> CalculateSgpaAsync(int studentId, int termId) => ic.CalculateSgpaAsync(studentId, termId);
        public Task<decimal> CalculateCgpaAsync(int studentId) => ic.CalculateCgpaAsync(studentId);
        public Task<TermGpaHistoryDto> GetTermGpaHistoryAsync(int studentId) => ic.GetTermGpaHistoryAsync(studentId);
        public Task<Dictionary<int, decimal>> BatchCalculateCgpaAsync(List<int> studentIds) => ic.BatchCalculateCgpaAsync(studentIds);
    }
}
