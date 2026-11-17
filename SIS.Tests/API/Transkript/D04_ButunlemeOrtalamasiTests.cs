using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.Transkript;

// D-04: FF sonrası bütünlemeyle CC'ye çıkan dersin transkript satırı.
public class D04_ButunlemeOrtalamasiTests
{
    // Bilinen hata D-04: transkript ders ortalaması bütünleme puanını (MakeupScore) okumuyor, eski final puanıyla hesaplıyor.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task D04_Butunleme_OrtalamaEskiFinalPuanylaHesaplaniyor()
    {
        using var ortam = new TranskriptOrtami();
        var (donem, final) = Kur(ortam);

        // 60×0,40 + 20×0,60 = 36,00 (bütünleme 80 okunsaydı 72,00)
        Assert.Equal(36.00m, TranskriptOrtami.Ders(await ortam.TranskriptAsync(), donem, "MAT101").AverageScore);

        // Karşı örnek: bütünleme puanı değişince ortalama değişmiyor, final puanı değişince değişiyor.
        final.MakeupScore = 10m;
        ortam.Db.SaveChanges();
        Assert.Equal(36.00m, TranskriptOrtami.Ders(await ortam.TranskriptAsync(), donem, "MAT101").AverageScore);
        final.Score = 30m;
        ortam.Db.SaveChanges();
        Assert.Equal(42.00m, TranskriptOrtami.Ders(await ortam.TranskriptAsync(), donem, "MAT101").AverageScore);
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

    private static (AcademicTerm Donem, StudentGrade Final) Kur(TranskriptOrtami ortam)
    {
        var donem = ortam.DonemEkle(2023, TermType.Fall);
        var ders = ortam.DersEkle("MAT101", 6);
        var kayit = ortam.KayitEkle(ders, donem, GradeCode.CC);
        ortam.PuanEkle(kayit, ortam.BilesenEkle(ders, donem, "Vize", 40m), 60m);
        return (donem, ortam.PuanEkle(kayit, ortam.BilesenEkle(ders, donem, "Final", 60m), 20m, butPuani: 80m));
    }
}
