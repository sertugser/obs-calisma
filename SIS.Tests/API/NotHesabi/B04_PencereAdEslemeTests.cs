using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Domain.Enums;

namespace SIS.Tests.API.NotHesabi;

/// <summary>
/// B-04: Not girişi "süre tanımlarına bağlı"; genel pencere açık değil, ara sınav penceresi açık,
/// final penceresi kapalı. Pencere, bileşen adından seçiliyor.
/// </summary>
public class B04_PencereAdEslemeTests
{
    // Bilinen hata H7: adında "ara" geçen bileşen ara sınav penceresine bağlanıyor.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task B04_ArastirmaOdevi_AraSinavPenceresindenKabulEdiliyor()
    {
        var (sonuc, kayitliPuan) = await PuanGonder("Araştırma Ödevi");

        Assert.IsType<OkObjectResult>(sonuc);
        Assert.Equal(85m, kayitliPuan);
    }

    // Karşı örnek: adında eşleşen sözcük olmayan bileşen genel pencereye düşüyor; o kapalı → 400.
    [Fact]
    public async Task B04_KarsiOrnek_ProjeGenelPencereyeDusuyor_400()
    {
        var (sonuc, kayitliPuan) = await PuanGonder("Proje");

        Assert.Equal(GenelPencereMesaji, RetMesaji(sonuc));
        Assert.Null(kayitliPuan);
    }

    // Bilinen hata H7 (büyük harf): ToLowerInvariant "İ"yi (U+0130) "i" yapmıyor; "VİZE" genel pencereye düşüyor.
    [Theory]
    [Trait("Durum", "BilinenHata")]
    [InlineData("Vize", true)]
    [InlineData("VIZE", true)]
    [InlineData("V\u0130ZE", false)]
    public async Task B04_VizeBuyukHarf_NoktaliIEslesmiyor(string bilesenAdi, bool kabul)
    {
        var (sonuc, kayitliPuan) = await PuanGonder(bilesenAdi);

        Assert.IsType(kabul ? typeof(OkObjectResult) : typeof(BadRequestObjectResult), sonuc);
        Assert.Equal(kabul ? 85m : (decimal?)null, kayitliPuan);
        if (!kabul)
            Assert.Equal(GenelPencereMesaji, RetMesaji(sonuc));
    }

    // Saplama: genel ve final penceresi farklı mesajla kapalı; ret mesajı reddin hangi pencereden geldiğini gösterir.
    private const string GenelPencereMesaji = "Genel pencere kapalı.";

    private static string RetMesaji(IActionResult sonuc) =>
        NotHesabiOrtami.Oku<string>(Assert.IsType<BadRequestObjectResult>(sonuc).Value!, "error");

    private static async Task<(IActionResult Sonuc, decimal? KayitliPuan)> PuanGonder(string bilesenAdi)
    {
        using var ortam = new NotHesabiOrtami();
        var bilesen = ortam.BilesenEkle(bilesenAdi, 20m, GradeComponentType.Other, 1);
        var (ogrenci, _, kayit) = ortam.OgrenciEkle("01");
        var pencereler = new SaplamaPencereServisi(new Dictionary<string, WebModuleAccessResult>
        {
            ["instructor-grade-entry"] = new(true, false, WebModuleControlModes.GradeEntryPeriod, "Kapalı", "Kapalı", GenelPencereMesaji),
            ["midterm-grade-entry"] = SaplamaPencereServisi.Acik(),
            ["final-grade-entry"] = new(true, false, WebModuleControlModes.Closed, "Kapalı", "Kapalı", "Final penceresi kapalı.")
        });

        var sonuc = await ortam.NotController(pencereler).SubmitGrades(ortam.Sube.Id, new GradeController.GradeSubmitDto
        {
            ComponentId = bilesen.Id,
            Grades = [new GradeController.StudentScoreDto { StudentId = ogrenci.Id, Score = 85m }]
        });

        var puan = await ortam.Db.StudentGrades.Where(g => g.CourseRegistrationId == kayit.Id)
            .Select(g => g.Score).SingleOrDefaultAsync();
        return (sonuc, puan);
    }
}
