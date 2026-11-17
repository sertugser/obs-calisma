using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;

namespace SIS.Tests.API.NotHesabi;

// B-03: Yönerge 1 ara sınav diyor; hoca iki ara sınav bileşeni ekleyebiliyor ve ders sonuçlandırılabiliyor.
public class B03_SinavSayisiTests
{
    // Bilinen hata H3: ara sınav sayısı bileşen eklemede ve sonuçlandırmada denetlenmiyor;
    // yalnız toplam ara sınav ağırlığı (%40–60) denetleniyor.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task B03_YonergeBirAraSinav_IkiAraSinavBileseniKabul_SonuclandirmaKabul()
    {
        using var ortam = new NotHesabiOrtami();
        ortam.HarfOlcutuEkle();
        ortam.YonergeEkle(y => { y.MidtermCount = 1; y.MinMidtermWeightPercent = 40m; y.MaxMidtermWeightPercent = 60m; });
        var controller = ortam.NotController();

        foreach (var (ad, agirlik, tur, sira) in new[]
                 {
                     ("Ara Sınav 1", 30m, GradeComponentType.Midterm, 1),
                     ("Ara Sınav 2", 30m, GradeComponentType.Midterm, 2),
                     ("Final", 40m, GradeComponentType.Final, 3)
                 })
        {
            var dto = new GradeController.ComponentDto { Name = ad, Weight = agirlik, Type = tur, Order = sira };
            Assert.IsType<OkObjectResult>(await controller.UpsertComponent(ortam.Sube.Id, dto));
        }

        var bilesenler = await ortam.Db.GradeComponents.ToListAsync();
        Assert.Equal(2, bilesenler.Count(b => b.Type == GradeComponentType.Midterm));

        var (_, _, kayit) = ortam.OgrenciEkle("01");
        foreach (var bilesen in bilesenler)
            ortam.PuanEkle(kayit, bilesen, 80m);

        Assert.IsType<OkObjectResult>(await controller.Finalize(ortam.Sube.Id));
        Assert.Equal(GradeCode.BB, (await ortam.Db.CourseRegistrations.FindAsync(kayit.Id))!.Grade); // 80 → BB
        Assert.True((await ortam.Db.CourseSections.FindAsync(ortam.Sube.Id))!.IsFinalized);
    }

    // Karşı örnek: kurulan yönerge bu ders için gerçekten "1 ara sınav" olarak çözülüyor.
    [Fact]
    public async Task B03_KurulumDogrulama_YonergeBirAraSinavCozuluyor()
    {
        using var ortam = new NotHesabiOrtami();
        ortam.YonergeEkle(y => y.MidtermCount = 1);

        var sayilar = await new ExamRegulationResolver(ortam.Db).ResolveCourseExamCountsAsync(ortam.Ders.Id, ortam.Donem.Id, default);

        Assert.True(sayilar.HasRegulation);
        Assert.Equal(1, sayilar.MidtermCount);
    }
}
