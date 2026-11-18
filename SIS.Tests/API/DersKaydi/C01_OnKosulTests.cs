using SIS.Domain.Enums;

namespace SIS.Tests.API.DersKaydi;

// C-01: X dersinin ön koşulu Y; plana ekleme kolu. (Doğrulama kolu RegistrationValidationServiceTests'te zaten sabit.)
public class C01_OnKosulTests
{
    [Fact]
    public async Task C01_PlanaEkleme_OnKosulGecilmemis_Reddediliyor()
    {
        using var ortam = new DersKaydiOrtami();
        var y = ortam.DersEkle("YYY101");
        var x = ortam.DersEkle("XXX201");
        ortam.OnKosulEkle(x, y);
        var sube = ortam.Sube(x);

        var yanit = await ortam.Kayit().AddToPlanAsync(ortam.Ogrenci.Id, sube.Id);

        Assert.False(yanit.Success);
        var hata = Assert.Single(yanit.ValidationResult!.Errors);
        Assert.Equal("PrerequisiteNotMet", hata.Rule);
        Assert.Equal(sube.Id, hata.CourseSectionId);
        Assert.Empty(ortam.Db.CourseRegistrations);
    }

    // Karşı örnek: ön koşul geçildiğinde (BB, CC) aynı ekleme taslak kayıt oluşturuyor; FF ve W geçmiş sayılmıyor.
    [Theory]
    [InlineData(GradeCode.BB, true)]
    [InlineData(GradeCode.CC, true)]
    [InlineData(GradeCode.FF, false)]
    [InlineData(GradeCode.W, false)]
    public async Task C01_PlanaEkleme_OnKosulNotunaGore(GradeCode not, bool kabul)
    {
        using var ortam = new DersKaydiOrtami();
        var y = ortam.DersEkle("YYY101");
        var x = ortam.DersEkle("XXX201");
        ortam.OnKosulEkle(x, y);
        ortam.NotEkle(y, not);
        var sube = ortam.Sube(x);

        var yanit = await ortam.Kayit().AddToPlanAsync(ortam.Ogrenci.Id, sube.Id);

        Assert.Equal(kabul, yanit.Success);
        var kayit = ortam.Db.CourseRegistrations.SingleOrDefault(r => r.CourseSectionId == sube.Id);
        Assert.Equal(kabul, kayit is not null);
        if (kabul)
            Assert.Equal(RegistrationStatus.Draft, kayit!.Status);
        else
            Assert.Equal("PrerequisiteNotMet", Assert.Single(yanit.ValidationResult!.Errors).Rule);
    }
}
