using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.Transkript;

// D-04: FF sonrası bütünlemeyle CC'ye çıkan dersin transkript satırı.
// Bütünleme/mazeret puanı (MakeupScore) varsa o bileşenin Score'unun yerine geçer, aynı ağırlıkla; "yüksek olan" kuralı yok.
public class D04_ButunlemeOrtalamasiTests
{
    [Fact]
    public async Task D04_Butunleme_OrtalamaButunlemePuanylaHesaplaniyor()
    {
        using var ortam = new TranskriptOrtami();
        var (donem, final) = Kur(ortam);

        // 60×0,40 + 80×0,60 = 24 + 48 = 72,00 (final 20 okunsaydı 36,00)
        Assert.Equal(72.00m, await OrtalamaAsync(ortam, donem));

        // Karşı örnek: bütünleme finalden düşük olsa da kullanılır: 24 + 10×0,60 = 24 + 6 = 30,00 (yüksek olan alınsaydı 36,00)
        final.MakeupScore = 10m;
        ortam.Db.SaveChanges();
        Assert.Equal(30.00m, await OrtalamaAsync(ortam, donem));
        // Bütünleme varken final puanı okunmaz: final 30 olunca yine 30,00
        final.Score = 30m;
        ortam.Db.SaveChanges();
        Assert.Equal(30.00m, await OrtalamaAsync(ortam, donem));
        // Bütünleme yoksa final puanı kullanılır: 24 + 30×0,60 = 24 + 18 = 42,00
        final.MakeupScore = null;
        ortam.Db.SaveChanges();
        Assert.Equal(42.00m, await OrtalamaAsync(ortam, donem));
    }

    // Harf notu ve katsayı kayıttaki (bütünleme sonrası) nottan geliyor.
    [Fact]
    public async Task D04_Butunleme_HarfKayittakiNottanGeliyor()
    {
        using var ortam = new TranskriptOrtami();
        var (donem, _) = Kur(ortam);

        var ders = TranskriptOrtami.Ders(await ortam.TranskriptAsync(), donem, "MAT101");
        Assert.Equal("CC", ders.Grade);
        Assert.True(ders.IsPassed);
        Assert.Equal(2.00m, ders.GradePoint);
    }

    // Final Score boş, bütünleme 0: satır elenmez, 0 puan sayılır.
    [Fact]
    public async Task D04_FinalPuaniBos_Butunleme0_OrtalamaHesaplaniyor()
    {
        using var ortam = new TranskriptOrtami();
        var (donem, final) = Kur(ortam, final: null, finalBut: 0m);

        // 60×0,40 + 0×0,60 = 24 + 0 = 24,00
        Assert.Equal(24.00m, await OrtalamaAsync(ortam, donem));

        // Karşı örnek: bütünleme de boşsa bileşen eksik, ortalama null
        final.MakeupScore = null;
        ortam.Db.SaveChanges();
        Assert.Null(await OrtalamaAsync(ortam, donem));
    }

    // Mazeret: vize Score boş, mazeret puanı 55; final 70 (bütünlemesiz).
    [Fact]
    public async Task D04_VizePuaniBos_Mazeret55_OrtalamaHesaplaniyor()
    {
        using var ortam = new TranskriptOrtami();
        var (donem, final) = Kur(ortam, vize: null, vizeBut: 55m, final: 70m, finalBut: null);

        // 55×0,40 + 70×0,60 = 22 + 42 = 64,00
        Assert.Equal(64.00m, await OrtalamaAsync(ortam, donem));

        // Karşı örnek: mazeret puanı da boşsa bileşen eksik, ortalama null
        ortam.Db.StudentGrades.Single(g => g.Id != final.Id).MakeupScore = null;
        ortam.Db.SaveChanges();
        Assert.Null(await OrtalamaAsync(ortam, donem));
    }

    // Yuvarlama (2 hane, AwayFromZero) bütünleme puanıyla aynı: %50/%50.
    [Fact]
    public async Task D04_Butunleme_YarimDegerUsteYuvarlaniyor()
    {
        using var ortam = new TranskriptOrtami();
        var (donem, final) = Kur(ortam, vize: 70.01m, final: 0m, finalBut: 70.00m, vizeAgirlik: 50m);

        // 70,01×0,50 + 70,00×0,50 = 35,005 + 35 = 70,005 → 70,01 (ToEven olsaydı 70,00)
        Assert.Equal(70.01m, await OrtalamaAsync(ortam, donem));

        // Karşı örnek: bütünleme yoksa final 0 okunur: 35,005 + 0 = 35,005 → 35,01
        final.MakeupScore = null;
        ortam.Db.SaveChanges();
        Assert.Equal(35.01m, await OrtalamaAsync(ortam, donem));
    }

    private static async Task<decimal?> OrtalamaAsync(TranskriptOrtami ortam, AcademicTerm donem) =>
        TranskriptOrtami.Ders(await ortam.TranskriptAsync(), donem, "MAT101").AverageScore;

    private static (AcademicTerm Donem, StudentGrade Final) Kur(TranskriptOrtami ortam, decimal? vize = 60m, decimal? vizeBut = null,
        decimal? final = 20m, decimal? finalBut = 80m, decimal vizeAgirlik = 40m)
    {
        var donem = ortam.DonemEkle(2023, TermType.Fall);
        var ders = ortam.DersEkle("MAT101", 6);
        var kayit = ortam.KayitEkle(ders, donem, GradeCode.CC);
        ortam.PuanEkle(kayit, ortam.BilesenEkle(ders, donem, "Vize", vizeAgirlik), vize, butPuani: vizeBut);
        return (donem, ortam.PuanEkle(kayit, ortam.BilesenEkle(ders, donem, "Final", 100m - vizeAgirlik), final, butPuani: finalBut));
    }
}
