using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.Transkript;

// D-06: transkriptin tam çıktısı (iki controller yanıtı, ortalama servisi, yeniden hesap ve öğrenci kaydı) altın kopyayla sabit.
// Altın kopya bilinen hataların bugünkü sonuçlarını da içerir; düzeltme ayrı pakette bu satırları bilerek günceller:
// - H2 (tekrarda W önceki CC'yi düşürüyor): 2023-2024 Spring ve 2024-2025 Fall dönemlerinin Cgpa ve Cumulative* satırları,
//   CurrentCgpa, TotalEarned*, TotalGpa*, aynı dönemlerin Gecmis.Terms[..].Cgpa satırları, CalculateCgpa, YenidenHesap.Cgpa,
//   YenidenHesap.TotalAkts, YenidenHesap.TotalPoints, Ogrenci.Cgpa.
// - D-04 (bütünleme puanı okunmuyor): Transkript.Terms[2023-2024 Fall].Courses[FIZ101].AverageScore.
public class D06_AltinKopyaTests
{
    [Fact]
    public async Task D06_TamCikti_AltinKopyaylaAyni()
    {
        using var ortam = new TranskriptOrtami();
        var donemler = Kur(ortam);
        var controller = ortam.Controller();
        var gpa = ortam.Gpa();
        var kopya = new AltinKopya(await ortam.Db.Courses.ToDictionaryAsync(c => c.Id, c => c.Code));

        kopya.Ekle("Transkript", Assert.IsType<OkObjectResult>(await controller.GetTranscript()).Value);
        kopya.Ekle("Gecmis", Assert.IsType<OkObjectResult>(await controller.GetTermGpaHistory()).Value);
        kopya.Ekle("CalculateCgpa", await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
        foreach (var donem in donemler[..3])
            kopya.Ekle($"CalculateSgpa[{donem.Name}]", await gpa.CalculateSgpaAsync(ortam.Ogrenci.Id, donem.Id));
        kopya.Ekle("YenidenHesap", await gpa.RecalculateAndUpdateAsync(ortam.Ogrenci.Id));
        var ogrenci = await OgrenciAsync(ortam);
        kopya.Ekle("Ogrenci.Cgpa", ogrenci.Cgpa);
        kopya.Ekle("Ogrenci.Sgpa", ogrenci.Sgpa);

        kopya.Karsilastir("D06_AltinKopya.txt");
    }

    // Üç yol eşitliği: transkriptteki genel ortalama = ortalama servisi = dönem geçmişinin son değeri = yeniden hesap sonrası kayıt.
    [Fact]
    public async Task D06_UcYolEsitligi_GenelOrtalamaAyni()
    {
        using var ortam = new TranskriptOrtami();
        Kur(ortam);
        ortam.Ogrenci.Cgpa = 1.11m; // karşı örnek: yeniden hesaptan önce saklanan değer farklı
        ortam.Ogrenci.Sgpa = 1.11m;
        ortam.Db.SaveChanges();
        var gpa = ortam.Gpa();
        var transkript = await ortam.TranskriptAsync();
        var gecmis = await gpa.GetTermGpaHistoryAsync(ortam.Ogrenci.Id);

        Assert.Equal(2.52m, transkript.CurrentCgpa);
        Assert.Equal(2.52m, await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
        Assert.Equal(2.52m, gecmis.Terms[^1].Cgpa);
        Assert.Equal(1.11m, (await OgrenciAsync(ortam)).Cgpa);
        await gpa.RecalculateAndUpdateAsync(ortam.Ogrenci.Id);
        var ogrenci = await OgrenciAsync(ortam);
        Assert.Equal(2.52m, ogrenci.Cgpa);
        // Saklanan ANO notlu son dönemin ANO'su: 45/13 = 3,46
        Assert.Equal(3.46m, ogrenci.Sgpa);
        Assert.Equal(3.46m, transkript.Terms[^1].Sgpa);
        Assert.Equal(3.46m, gecmis.Terms[^1].Sgpa);

        // Dönem ortalaması genel ortalamadan farklı (2023-2024 Spring): ANO 18/11 = 1,64; AGNO 32/20 = 1,60
        Assert.Equal(1.64m, transkript.Terms[1].Sgpa);
        Assert.Equal(1.60m, transkript.Terms[1].Cgpa);
        Assert.Equal(1.64m, gecmis.Terms[1].Sgpa);
        Assert.Equal(1.60m, gecmis.Terms[1].Cgpa);
    }

    // Karşı örnek: aktif dönemin notu ilan edilmemişken transkript ve dönem geçmişi notu gizliyor,
    // karar hesapları (CalculateCgpa, yeniden hesap) sayıyor; üç yol eşitliği yalnız not görünürken geçerli.
    [Fact]
    public async Task D06_AktifDonemNotuGizliyken_YollarAyrisiyor()
    {
        using var ortam = new TranskriptOrtami();
        var donemler = Kur(ortam);
        ortam.KayitEkle(ortam.DersEkle("OPT101", 4), donemler[3], GradeCode.AA);
        var kapali = new Mock<IWebModuleControlService>();
        kapali.Setup(s => s.EvaluateAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebModuleAccessResult(true, false, WebModuleControlModes.Closed, "Kapalı", "Kapalı", null));
        var gpa = ortam.Gpa(kapali.Object);

        var transkript = await ortam.TranskriptAsync(kapali.Object);
        Assert.Equal(2.52m, transkript.CurrentCgpa);
        Assert.Equal(3, transkript.Terms.Count);
        Assert.Equal(2.52m, (await gpa.GetTermGpaHistoryAsync(ortam.Ogrenci.Id)).Terms[^1].Cgpa);
        // (73 + 4×4,00) / (29 + 4) = 89/33 = 2,70
        Assert.Equal(2.70m, await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
        var sonuc = await gpa.RecalculateAndUpdateAsync(ortam.Ogrenci.Id);
        Assert.Equal((2.70m, 4.00m), (sonuc.Cgpa, sonuc.Sgpa));
    }

    private static Task<Student> OgrenciAsync(TranskriptOrtami ortam) =>
        ortam.Db.Students.AsNoTracking().SingleAsync(s => s.Id == ortam.Ogrenci.Id);

    // 3 tamamlanmış dönem, 1 aktif dönem, 13 kayıt. Elle hesap: ANO 1,24 / 1,64 / 3,46; AGNO 1,24 / 1,60 / 2,52.
    private static AcademicTerm[] Kur(TranskriptOrtami o)
    {
        AcademicTerm[] d = [o.DonemEkle(2023, TermType.Fall), o.DonemEkle(2023, TermType.Spring),
            o.DonemEkle(2024, TermType.Fall), o.AktifDonemEkle(2024, TermType.Spring)];
        var mat = o.DersEkle("MAT101", 6, kredi: 4);
        var prg = o.DersEkle("PRG101", 6, kredi: 4);
        var esk = o.DersEkle("ESK101", 4);
        var yen = o.DersEkle("YEN101", 4);
        o.EsdegerEkle(esk, yen);
        var gpaDisi = o.Kaydet(new CurriculumCourseOpeningReason { Code = "DN", Name = "Deneme Nedeni", IsExcludedFromGpa = true });

        o.KayitEkle(mat, d[0], GradeCode.FF);                    // tekrarın ilk hâli
        var fiz = o.DersEkle("FIZ101", 5);
        var fizKayit = o.KayitEkle(fiz, d[0], GradeCode.CC);     // bütünlemeyle CC: final 20, bütünleme 80
        o.PuanEkle(fizKayit, o.BilesenEkle(fiz, d[0], "Vize", 40m), 60m);
        o.PuanEkle(fizKayit, o.BilesenEkle(fiz, d[0], "Final", 60m), 20m, butPuani: 80m);
        o.KayitEkle(prg, d[0], GradeCode.CC);                    // sonraki dönem W ile tekrar
        o.KayitEkle(esk, d[0], GradeCode.DD);                    // eşdeğer zincirin eski dersi
        o.KayitEkle(o.DersEkle("ING101", 2, kredi: 1, notsuz: true), d[0], GradeCode.S);

        o.KayitEkle(mat, d[1], GradeCode.BB);                    // tekrar (bayraksız)
        o.KayitEkle(prg, d[1], GradeCode.W, RegistrationStatus.Withdrawn);
        o.KayitEkle(o.DersEkle("TAR101", 3, kredi: 2), d[1], GradeCode.CC, ayarla: k => k.OpeningReasonSnapshot = gpaDisi);
        o.KayitEkle(o.DersEkle("KIM101", 5), d[1], GradeCode.NA);

        o.KayitEkle(yen, d[2], GradeCode.BA);                    // ESK101'in eşdeğeri
        o.KayitEkle(o.DersEkle("BIL101", 5), d[2], GradeCode.AA, ayarla: k => k.EctsSnapshot = 4);
        var end = o.DersEkle("END101", 5);
        var endKayit = o.KayitEkle(end, d[2], GradeCode.BB);     // eksik bileşen: final puanı yok
        o.PuanEkle(endKayit, o.BilesenEkle(end, d[2], "Vize", 40m), 70m);
        o.BilesenEkle(end, d[2], "Final", 60m);

        o.KayitEkle(o.DersEkle("SEM101", 3, kredi: 2), d[3], null, RegistrationStatus.Active); // aktif dönemde notsuz
        return d;
    }
}
