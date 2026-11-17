using System.Globalization;
using SIS.Domain.Enums;

namespace SIS.Tests.API.Transkript;

// D-05 (transkript tarafı): aynı ders tekrarı (FF→BB) ve eşdeğer ders zincirinde (ESK101 DD → YEN101 BA) yalnız son not sayılıyor;
// transkript servisinin kendi döngüsü ortalama servisiyle aynı sonucu veriyor.
public class D05_TekrarZinciriTests
{
    // Eşdeğerlik varken: (BB 6×3,00 + BA 4×3,50 + CC 5×2,00) / 15 = 42/15 = 2,80; kredi 4 + 3 + 3.
    // Karşı örnek, eşdeğerlik yok: ESK101 DD de giriyor: 46/19 = 2,42; kredi 13.
    [Theory]
    [InlineData(true, "2.80", 15, 10)]
    [InlineData(false, "2.42", 19, 13)]
    public async Task D05_TekrarVeEsdegerZinciri_TranskriptteYalnizSonNot(bool esdeger, string beklenen, int gpaAkts, int kredi)
    {
        using var ortam = new TranskriptOrtami();
        // Ters sırada eklenir: sonraki dönem ve kayıtları daha küçük Id alıyor; "son" dönem başlangıcına göre seçiliyor.
        var bahar = ortam.DonemEkle(2023, TermType.Spring);
        var guz = ortam.DonemEkle(2023, TermType.Fall);
        var mat = ortam.DersEkle("MAT101", 6, kredi: 4);
        var esk = ortam.DersEkle("ESK101", 4);
        var yen = ortam.DersEkle("YEN101", 4);
        if (esdeger)
            ortam.EsdegerEkle(esk, yen);
        ortam.KayitEkle(mat, bahar, GradeCode.BB);
        ortam.KayitEkle(yen, bahar, GradeCode.BA);
        ortam.KayitEkle(mat, guz, GradeCode.FF);
        ortam.KayitEkle(esk, guz, GradeCode.DD);
        ortam.KayitEkle(ortam.DersEkle("DIG101", 5), guz, GradeCode.CC);
        var agno = decimal.Parse(beklenen, CultureInfo.InvariantCulture);
        var gpa = ortam.Gpa();

        var transkript = await ortam.TranskriptAsync();
        Assert.Equal(guz.Id, transkript.Terms[0].TermId);
        Assert.Equal(agno, transkript.CurrentCgpa);
        Assert.Equal(agno, transkript.Terms[^1].Cgpa);
        Assert.Equal(agno, await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
        Assert.Equal(agno, (await gpa.GetTermGpaHistoryAsync(ortam.Ogrenci.Id)).Terms[^1].Cgpa);
        Assert.Equal(gpaAkts, transkript.TotalGpaEcts);
        Assert.Equal(kredi, transkript.TotalEarnedCredits);
        Assert.Equal(kredi, transkript.TotalAttemptedCredits);

        Assert.True(TranskriptOrtami.Ders(transkript, guz, "MAT101").IsRepeated);
        Assert.True(TranskriptOrtami.Ders(transkript, bahar, "MAT101").IsRepeated);
        Assert.Equal(esdeger, TranskriptOrtami.Ders(transkript, guz, "ESK101").IsRepeated);
        Assert.Equal(esdeger, TranskriptOrtami.Ders(transkript, bahar, "YEN101").IsRepeated);
        Assert.False(TranskriptOrtami.Ders(transkript, guz, "DIG101").IsRepeated);
        Assert.Equal(esdeger ? new List<int> { yen.Id } : new List<int>(),
            TranskriptOrtami.Ders(transkript, guz, "ESK101").EquivalentCourseIds);
    }
}
