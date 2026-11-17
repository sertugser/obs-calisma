using Microsoft.AspNetCore.Mvc;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.NotHesabi;

// B-06: Teori 3 saat, 14 hafta, sınır %30 → 13 saat. Sınırı aşan öğrenci (5 oturum × 3 = 15) NA alıyor;
// kayıttaki devam zorunluluğu (MandatoryAttendance) sonucu değiştirmiyor.
public class B06_DevamMuafiyetiTests
{
    // Bilinen hata H1: sonuçlandırma devam muafiyetine bakmıyor; muaf kayıt da devamsızlıktan NA alıyor.
    [Theory]
    [Trait("Durum", "BilinenHata")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task B06_SinirAsilinca_ZorunlulukNeOlursaOlsunNA(bool devamZorunlu)
    {
        using var ortam = new NotHesabiOrtami();
        ortam.Ders.TheoryHours = 3;
        var vize = ortam.BilesenEkle("Vize", 40m, GradeComponentType.Midterm, 1);
        var final = ortam.BilesenEkle("Final", 60m, GradeComponentType.Final, 2);
        ortam.HarfOlcutuEkle();
        var oturum = new AttendanceSession { CourseSection = ortam.Sube, Instructor = ortam.Sube.Instructor!, WeekNumber = 1, SessionDate = new DateTime(2024, 10, 7) };
        var kayitlar = new List<CourseRegistration>();
        foreach (var (no, devamsizOturum) in new[] { ("01", 5), ("02", 4) })
        {
            var (ogrenci, _, kayit) = ortam.OgrenciEkle(no);
            kayit.MandatoryAttendance = devamZorunlu;
            ortam.PuanEkle(kayit, vize, 80m);
            ortam.PuanEkle(kayit, final, 80m);
            for (var i = 0; i < devamsizOturum; i++)
                ortam.Db.AttendanceRecords.Add(new AttendanceRecord { AttendanceSession = oturum, CourseRegistrationId = kayit.Id, StudentId = ogrenci.Id, Status = AttendanceStatus.Absent, TotalSlotCount = 3 });
            kayitlar.Add(kayit);
        }
        ortam.Db.SaveChanges();

        Assert.IsType<OkObjectResult>(await ortam.NotController().Finalize(ortam.Sube.Id));

        Assert.Equal(GradeCode.NA, kayitlar[0].Grade);
        // Karşı örnek: sınırı aşmayan öğrenci (4 × 3 = 12) puanından harf alıyor.
        Assert.Equal(GradeCode.BB, kayitlar[1].Grade);
    }
}
