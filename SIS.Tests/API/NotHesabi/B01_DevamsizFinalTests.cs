using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SIS.API.Controllers;
using SIS.Application.CalendarGenerator.Services;
using SIS.Application.Common.DTOs;
using SIS.Application.Common.Services;
using SIS.Application.CourseRegistration.Services;
using SIS.Application.GpaCalculation.Services;
using SIS.Application.StudentCard.Services;
using SIS.Domain.Enums;

namespace SIS.Tests.API.NotHesabi;

// B-01: Vize %40 ile 70, finale girmedi (Absent, puan boş), yönerge yok. Aynı öğrenci için üç yüzey üç farklı sonuç veriyor.
public class B01_DevamsizFinalTests
{
    // Bilinen hata D-1 (ortalama kopyaları): ders ortalaması hoca listesi, sonuçlandırma ve öğrenci ekranında
    // ayrı ayrı hesaplanıyor. Düzeltme ayrı pakette bu testi bilerek günceller.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task B01_VizeYetmisFinalGirmedi_HocaListesi70_Sonuclandirma28FF_OgrenciEkraniNull()
    {
        using var ortam = new NotHesabiOrtami();
        var vize = ortam.BilesenEkle("Vize", 40m, GradeComponentType.Midterm, 1);
        var final = ortam.BilesenEkle("Final", 60m, GradeComponentType.Final, 2);
        ortam.HarfOlcutuEkle();
        var (_, ogrenciKullanici, kayit) = ortam.OgrenciEkle("01");
        ortam.PuanEkle(kayit, vize, 70m);
        ortam.PuanEkle(kayit, final, null, ExamAttendance.Absent);
        var notController = ortam.NotController();

        // (1) Hoca listesi: yalnız puanı olan bileşenler, Σw'ye bölünüyor
        var liste = Assert.IsType<OkObjectResult>(await notController.GetGrades(ortam.Sube.Id));
        var satir = Assert.Single(NotHesabiOrtami.Oku<IEnumerable>(liste.Value!, "Students").Cast<object>());
        Assert.Equal(70.00m, NotHesabiOrtami.Oku<decimal?>(satir, "WeightedAverage"));

        // (2) Sonuçlandırma: Absent → 0
        var sonuc = Assert.IsType<OkObjectResult>(await notController.Finalize(ortam.Sube.Id));
        var sonucSatiri = Assert.Single(NotHesabiOrtami.Oku<IEnumerable>(sonuc.Value!, "Results").Cast<object>());
        Assert.Equal(28.00m, NotHesabiOrtami.Oku<decimal>(sonucSatiri, "WeightedAverage"));
        Assert.Equal("FF", NotHesabiOrtami.Oku<string>(sonucSatiri, "LetterGrade"));
        Assert.Equal(GradeCode.FF, (await ortam.Db.CourseRegistrations.FindAsync(kayit.Id))!.Grade);

        // (3) Öğrenci ekranı: tüm bileşenlerde puan zorunlu; bileşenler yayımlı ve pencere açık
        var ogrenciController = new StudentController(ortam.Db, Mock.Of<IGpaCalculationService>(),
            Mock.Of<IAcademicCalendarRuleService>(), new NullSystemConfigProvider(), Mock.Of<ICurriculumStatusService>(),
            Mock.Of<IStudentTranscriptService>(), Mock.Of<IEctsLimitResolver>(), Mock.Of<ITranscriptScenarioService>(),
            new NullWebModuleControlService())
        {
            ControllerContext = NotHesabiOrtami.KullaniciBaglami(ogrenciKullanici.Id)
        };
        var ekran = Assert.IsType<OkObjectResult>(await ogrenciController.GetGradeStatistics(kayit.Id));
        var dto = Assert.IsType<StudentGradeStatisticsDto>(ekran.Value);
        Assert.Null(dto.StudentWeightedAverage);
        Assert.Equal(70m, dto.Components.Single(c => c.ComponentId == vize.Id).StudentScore);
    }
}
