using System.Globalization;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.DersKaydi;

// C-02: A≡B ve B≡C eşdeğer; öğrenci yalnız C'yi geçmiş; X dersinin ön koşulu A.
// Bugünkü davranış: ön koşul denetimi eşdeğerliği tek adım izliyor; ortalama zinciri ise geçişli.
public class C02_EsdegerlikTests
{
    private static (Course A, Course B, Course C, Course X) Kur(DersKaydiOrtami ortam)
    {
        var a = ortam.DersEkle("AAA101");
        var b = ortam.DersEkle("BBB101");
        var c = ortam.DersEkle("CCC101");
        var x = ortam.DersEkle("XXX201");
        ortam.OnKosulEkle(x, a);
        return (a, b, c, x);
    }

    [Fact]
    public async Task C02_OnKosul_ZincirEsdeger_KarsilanmiyorTekAdim()
    {
        using var ortam = new DersKaydiOrtami();
        var (a, b, c, x) = Kur(ortam);
        ortam.EsdegerEkle(a, b);
        ortam.EsdegerEkle(b, c);
        ortam.NotEkle(c, GradeCode.BB);
        var sube = ortam.Sube(x);

        var dogrulama = await ortam.Dogrulama().ValidateRegistrationAsync(ortam.Ogrenci.Id, [sube.Id]);
        var plan = await ortam.Kayit().AddToPlanAsync(ortam.Ogrenci.Id, sube.Id);

        Assert.Contains(dogrulama.Errors, e => e.Rule == "Prerequisite");
        Assert.False(plan.Success);
        Assert.Equal("PrerequisiteNotMet", Assert.Single(plan.ValidationResult!.Errors).Rule);
    }

    // Karşı örnek: A≡C doğrudan tanımlıysa (iki satır yönünde de) ön koşul karşılanıyor.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task C02_OnKosul_TekAdimEsdeger_Karsilaniyor(bool ac)
    {
        using var ortam = new DersKaydiOrtami();
        var (a, _, c, x) = Kur(ortam);
        if (ac) ortam.EsdegerEkle(a, c); else ortam.EsdegerEkle(c, a);
        ortam.NotEkle(c, GradeCode.BB);
        var sube = ortam.Sube(x);

        var dogrulama = await ortam.Dogrulama().ValidateRegistrationAsync(ortam.Ogrenci.Id, [sube.Id]);
        var plan = await ortam.Kayit().AddToPlanAsync(ortam.Ogrenci.Id, sube.Id);

        Assert.DoesNotContain(dogrulama.Errors, e => e.Rule == "Prerequisite");
        Assert.True(plan.Success);
    }

    // Satır yönü: (CourseId=A, EquivalentCourseId=C) satırı var, öğrenci geçmiş dersi aldı. Doğrulama eşdeğerliği iki yönde
    // okuyor; katalog yalnız satırın CourseId tarafından okuyor: ön koşul A, geçilen C ise katalog "karşılandı";
    // ön koşul C, geçilen A ise katalog "eksik" diyor (doğrulama iki durumda da karşılanmış sayıyor).
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task C02_Esdegerlik_KatalogYonluDogrulamaCiftYonlu(bool onKosulSatirdaCourseId, bool katalogKarsilandi)
    {
        using var ortam = new DersKaydiOrtami();
        var a = ortam.DersEkle("AAA101");
        var c = ortam.DersEkle("CCC101");
        var x = ortam.DersEkle("XXX201");
        ortam.EsdegerEkle(a, c);
        ortam.OnKosulEkle(x, onKosulSatirdaCourseId ? a : c);
        ortam.NotEkle(onKosulSatirdaCourseId ? c : a, GradeCode.BB);
        ortam.MufredataEkle(x, 3);
        var sube = ortam.Sube(x);

        var dogrulama = await ortam.Dogrulama().ValidateRegistrationAsync(ortam.Ogrenci.Id, [sube.Id]);
        var katalog = DersKaydiOrtami.Satir(await ortam.KatalogAsync(), "XXX201");

        Assert.DoesNotContain(dogrulama.Errors, e => e.Rule == "Prerequisite");
        Assert.Equal(katalogKarsilandi, katalog.PrerequisitesMet);
    }

    // Ortalama zinciri: A önceki dönemde FF, C sonra BB. A≡B ve B≡C varken ve öğrenci B'yi de almışsa A, B, C aynı grupta;
    // yalnız son not (BB) giriyor: 3,00. B hiç alınmamışsa zincir A ile C arasında kopuyor ve FF de giriyor:
    // (5×0 + 5×3,00) / 10 = 1,50. Karşı örnek: B alınmış ama B≡C bağı yoksa yine 1,50.
    [Theory]
    [InlineData(true, true, "3.00")]
    [InlineData(false, true, "1.50")]
    [InlineData(true, false, "1.50")]
    public async Task C02_Ortalama_ZincirGecisli(bool bAlinmis, bool bcBagi, string beklenen)
    {
        using var ortam = new DersKaydiOrtami();
        var (a, b, c, _) = Kur(ortam);
        ortam.EsdegerEkle(a, b);
        if (bcBagi) ortam.EsdegerEkle(b, c);
        ortam.NotEkle(a, GradeCode.FF, ortam.OncekiDonem);
        if (bAlinmis) ortam.NotEkle(b, GradeCode.FF, ortam.OncekiDonem);
        ortam.NotEkle(c, GradeCode.BB, ortam.GecmisDonem);

        var agno = await ortam.Gpa().CalculateCgpaAsync(ortam.Ogrenci.Id);

        Assert.Equal(decimal.Parse(beklenen, CultureInfo.InvariantCulture), agno);
    }
}
