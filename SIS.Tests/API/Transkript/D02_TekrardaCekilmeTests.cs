using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.Transkript;

// D-02: CC ile geçilen ders sonraki dönem tekrar alınıp W ile çekiliyor.
public class D02_TekrardaCekilmeTests
{
    // Bilinen hata H2: W son kayıt sayılıyor; önceki CC hem AGNO'dan hem kazanılan krediden düşüyor.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task D02_TekrardaW_OncekiCC_AgnodanVeKredidenDusuyor()
    {
        using var ortam = new TranskriptOrtami();
        var (prg, _, ikinci) = Kur(ortam);
        var gpa = ortam.Gpa();

        // Karşı örnek: W kaydı yokken CC sayılıyor: (6×2,00 + 4×3,00) / 10 = 2,40; kredi 4 + 3
        Assert.Equal(2.40m, await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
        Assert.Equal(7, (await ortam.TranskriptAsync()).TotalEarnedCredits);

        ortam.KayitEkle(prg, ikinci, GradeCode.W, RegistrationStatus.Withdrawn, k => k.IsRepeated = true);

        Assert.Equal(3.00m, await gpa.CalculateCgpaAsync(ortam.Ogrenci.Id));
        Assert.Equal(3.00m, (await gpa.GetTermGpaHistoryAsync(ortam.Ogrenci.Id)).Terms[^1].Cgpa);
        var transkript = await ortam.TranskriptAsync();
        Assert.Equal(3.00m, transkript.CurrentCgpa);
        Assert.Equal(3, transkript.TotalEarnedCredits);
        Assert.Equal(4, transkript.TotalEarnedEcts);
        Assert.Equal(4, transkript.TotalGpaEcts);
    }

    // W satırı transkriptte görünüyor: geçmedi, ortalama dışı, tekrar işaretli; dönem ANO'su 0, geçmişte null.
    [Fact]
    public async Task D02_TekrardaW_TranskriptteIkiSatirGorunuyor()
    {
        using var ortam = new TranskriptOrtami();
        var (prg, ilk, ikinci) = Kur(ortam);
        ortam.KayitEkle(prg, ikinci, GradeCode.W, RegistrationStatus.Withdrawn, k => k.IsRepeated = true);

        var transkript = await ortam.TranskriptAsync();
        Assert.Equal(2, transkript.Terms.Count);
        var cc = TranskriptOrtami.Ders(transkript, ilk, "PRG101");
        Assert.Equal("CC", cc.Grade);
        Assert.True(cc.CountsTowardsGpa);
        Assert.True(cc.IsRepeated);
        var w = TranskriptOrtami.Ders(transkript, ikinci, "PRG101");
        Assert.Equal("W", w.Grade);
        Assert.Equal("Withdrawn", w.Status);
        Assert.False(w.IsPassed);
        Assert.False(w.CountsTowardsGpa);
        Assert.Null(w.GradePoint);
        Assert.True(w.IsRepeated);
        Assert.Equal(0m, transkript.Terms[1].Sgpa);
        Assert.Null((await ortam.Gpa().GetTermGpaHistoryAsync(ortam.Ogrenci.Id)).Terms[1].Sgpa);
    }

    private static (Course Prg, AcademicTerm Ilk, AcademicTerm Ikinci) Kur(TranskriptOrtami ortam)
    {
        var ilk = ortam.DonemEkle(2023, TermType.Fall);
        var prg = ortam.DersEkle("PRG101", 6, kredi: 4);
        ortam.KayitEkle(prg, ilk, GradeCode.CC);
        ortam.KayitEkle(ortam.DersEkle("MAT101", 4, kredi: 3), ilk, GradeCode.BB);
        return (prg, ilk, ortam.DonemEkle(2023, TermType.Spring));
    }
}
