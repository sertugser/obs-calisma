using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Enums;

namespace SIS.Tests.API.DersKaydi;

// C-03: X dersinin eş-koşulu Z; öğrenci Z'yi aynı dönemde planına eklemiş.
public class C03_EsKosulTests
{
    private static async Task<(DersKaydiOrtami Ortam, int XSubeId)> KurAsync(bool zPlanda)
    {
        var ortam = new DersKaydiOrtami();
        var z = ortam.DersEkle("ZZZ201");
        var x = ortam.DersEkle("XXX201");
        ortam.OnKosulEkle(x, z, esKosul: true);
        ortam.MufredataEkle(x, 3);
        ortam.MufredataEkle(z, 3);
        var zSube = ortam.Sube(z);
        var xSube = ortam.Sube(x);
        if (zPlanda)
            Assert.True((await ortam.Kayit().AddToPlanAsync(ortam.Ogrenci.Id, zSube.Id)).Success);
        return (ortam, xSube.Id);
    }

    // Bilinen hata H8: katalog eş-koşulu tanımıyor; aynı dönemde planda olan Z'yi yok sayıp "önkoşul eksik" gösteriyor.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task C03_Katalog_EsKosulPlandaOlsaDaOnKosulEksikGosteriyor()
    {
        var (ortam, _) = await KurAsync(zPlanda: true);
        using var _ = ortam;

        var katalog = await ortam.KatalogAsync();
        var satir = DersKaydiOrtami.Satir(katalog, "XXX201");

        Assert.Equal("Kayıtlı", DersKaydiOrtami.Satir(katalog, "ZZZ201").ActiveRegistrationStatus);
        Assert.False(satir.PrerequisitesMet);
        Assert.Equal(["ZZZ201"], satir.MissingPrerequisites);
    }

    // Karşı örnek: Z geçilmişse katalog X'i karşılanmış gösteriyor (kurulum canlı; satır ve ön koşul okunuyor).
    [Fact]
    public async Task C03_Katalog_EsKosulGecilmisseKarsilaniyor()
    {
        var (ortam, _) = await KurAsync(zPlanda: false);
        using var _ = ortam;
        ortam.NotEkle(ortam.Db.Courses.Single(c => c.Code == "ZZZ201"), GradeCode.BB);

        var satir = DersKaydiOrtami.Satir(await ortam.KatalogAsync(), "XXX201");

        Assert.True(satir.PrerequisitesMet);
        Assert.Empty(satir.MissingPrerequisites);
    }

    // Gönderim eş-koşulu tanıyor: Z planda (taslak) iken X tek başına gönderilince kabul ediliyor.
    // Karşı örnek: Z planda değilken aynı gönderim "Prerequisite" ile reddediliyor.
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task C03_Gonderim_EsKosulPlandaysaKabulEdiliyor(bool zPlanda, bool kabul)
    {
        var (ortam, xSubeId) = await KurAsync(zPlanda);
        using var _ = ortam;

        var yanit = await ortam.Kayit().SubmitRegistrationAsync(ortam.Ogrenci.Id, new RegistrationSubmitDto { CourseSectionIds = [xSubeId] });

        Assert.Equal(kabul, yanit.Success);
        if (!kabul)
            Assert.Contains(yanit.ValidationResult!.Errors, e => e.Rule == "Prerequisite");
    }
}
