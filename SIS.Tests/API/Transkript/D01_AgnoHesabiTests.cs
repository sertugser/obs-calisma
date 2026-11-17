using System.Globalization;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.Transkript;

// D-01: ANO/AGNO = Σ(AKTS × katsayı) / Σ AKTS, 2 hane, yarım değer üste (AwayFromZero).
public class D01_AgnoHesabiTests
{
    // BB×1 + CC×7 = 17/8 = 2,125 → 2,13 (ToEven olsaydı 2,12). 4+4 AKTS karşı örnek: 2,50.
    [Theory]
    [InlineData(1, 7, "2.13")]
    [InlineData(4, 4, "2.50")]
    public async Task D01_YarimDeger_UsteYuvarlaniyor(int bbAkts, int ccAkts, string beklenen)
    {
        using var ortam = new TranskriptOrtami();
        var donem = ortam.DonemEkle(2023, TermType.Fall);
        ortam.KayitEkle(ortam.DersEkle("DNA101", bbAkts), donem, GradeCode.BB);
        ortam.KayitEkle(ortam.DersEkle("DNB101", ccAkts), donem, GradeCode.CC);
        var agno = decimal.Parse(beklenen, CultureInfo.InvariantCulture);
        var gpa = ortam.Gpa();

        Assert.Equal(agno, await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
        Assert.Equal(agno, await gpa.CalculateSgpaAsync(ortam.Ogrenci.Id, donem.Id));
        var gecmis = Assert.Single((await gpa.GetTermGpaHistoryAsync(ortam.Ogrenci.Id)).Terms);
        Assert.Equal(agno, gecmis.Sgpa);
        Assert.Equal(agno, gecmis.Cgpa);
        var transkript = await ortam.TranskriptAsync();
        Assert.Equal(agno, transkript.CurrentCgpa);
        Assert.Equal(agno, Assert.Single(transkript.Terms).Sgpa);
    }

    // S notu ve GPA dışı açma nedeni ortalamaya girmiyor; kayıttaki AKTS kataloğun önüne geçiyor.
    [Fact]
    public async Task D01_SNotu_GpaDisiAcmaNedeni_AktsKaydi()
    {
        using var ortam = new TranskriptOrtami();
        var donem = ortam.DonemEkle(2023, TermType.Fall);
        var acmaNedeni = ortam.Kaydet(new CurriculumCourseOpeningReason { Code = "DN", Name = "Deneme Nedeni", IsExcludedFromGpa = true });
        ortam.KayitEkle(ortam.DersEkle("MAT101", 6), donem, GradeCode.BB);
        ortam.KayitEkle(ortam.DersEkle("ING101", 2, notsuz: true), donem, GradeCode.S);
        ortam.KayitEkle(ortam.DersEkle("TAR101", 3), donem, GradeCode.CC, ayarla: k => k.OpeningReasonSnapshot = acmaNedeni);
        ortam.KayitEkle(ortam.DersEkle("BIL101", 5), donem, GradeCode.AA, ayarla: k => k.EctsSnapshot = 4);
        var gpa = ortam.Gpa();

        // (6×3,00 + 4×4,00) / 10 = 3,40
        Assert.Equal(3.40m, await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
        var transkript = await ortam.TranskriptAsync();
        Assert.Equal(3.40m, transkript.CurrentCgpa);
        Assert.Equal(10, transkript.TotalGpaEcts);
        Assert.Equal(15, transkript.TotalEarnedEcts);
        var ing = TranskriptOrtami.Ders(transkript, donem, "ING101");
        Assert.True(ing.IsPassed);
        Assert.False(ing.CountsTowardsGpa);
        Assert.False(TranskriptOrtami.Ders(transkript, donem, "TAR101").CountsTowardsGpa);
        Assert.Equal(4, TranskriptOrtami.Ders(transkript, donem, "BIL101").Ects);

        // Karşı örnek: açma nedeni GPA dışı değilse TAR101 giriyor: (18 + 6 + 16) / 13 = 3,08
        acmaNedeni.IsExcludedFromGpa = false;
        ortam.Db.SaveChanges();
        Assert.Equal(3.08m, await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
    }

    // Olası hata adayı (karar bekliyor): IsNonGpa işaretli ders harf notu alırsa ortalamaya giriyor.
    [Fact]
    public async Task D01_IsNonGpaDers_HarfNotuylaOrtalamayaGiriyor()
    {
        using var ortam = new TranskriptOrtami();
        var donem = ortam.DonemEkle(2023, TermType.Fall);
        var notsuzKayit = ortam.KayitEkle(ortam.DersEkle("SEC101", 4, notsuz: true), donem, GradeCode.BB);
        ortam.KayitEkle(ortam.DersEkle("MAT101", 4), donem, GradeCode.CC);
        var gpa = ortam.Gpa();

        // (4×3,00 + 4×2,00) / 8 = 2,50
        Assert.Equal(2.50m, await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
        Assert.True(TranskriptOrtami.Ders(await ortam.TranskriptAsync(), donem, "SEC101").CountsTowardsGpa);

        // Karşı örnek: aynı ders S notuyla ortalamaya girmiyor.
        notsuzKayit.Grade = GradeCode.S;
        ortam.Db.SaveChanges();
        Assert.Equal(2.00m, await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
    }
}
