using SIS.Domain.Enums;

namespace SIS.Tests.API.Transkript;

// D-03: transkriptteki ders ortalaması (StudentTranscriptService): Σ(puan × ağırlık) / Σ ağırlık, 2 hane AwayFromZero.
public class D03_DersOrtalamasiTests
{
    [Fact]
    public async Task D03_TumBilesenlerGirilmis_IkiHaneUsteYuvarlaniyor()
    {
        using var ortam = new TranskriptOrtami();
        var donem = ortam.DonemEkle(2023, TermType.Fall);
        // (70,01×50 + 70,00×50) / 100 = 70,005 → 70,01 (ToEven olsaydı 70,00)
        Kur(ortam, donem, "DNA101", (50m, 70.01m), (50m, 70.00m));
        // Karşı örnek, yarım olmayan değer: (80×50 + 61×50) / 100 = 70,50
        Kur(ortam, donem, "DNB101", (50m, 80m), (50m, 61m));
        // Ağırlık toplamı 100 değil: (70×40 + 50×40) / 80 = 60,00
        Kur(ortam, donem, "DNC101", (40m, 70m), (40m, 50m));

        var transkript = await ortam.TranskriptAsync();
        Assert.Equal(70.01m, TranskriptOrtami.Ders(transkript, donem, "DNA101").AverageScore);
        Assert.Equal(70.50m, TranskriptOrtami.Ders(transkript, donem, "DNB101").AverageScore);
        Assert.Equal(60.00m, TranskriptOrtami.Ders(transkript, donem, "DNC101").AverageScore);
    }

    [Fact]
    public async Task D03_BirBileseniBos_OrtalamaNull()
    {
        using var ortam = new TranskriptOrtami();
        var donem = ortam.DonemEkle(2023, TermType.Fall);
        Kur(ortam, donem, "SATIRYOK", (40m, 70m), (60m, null), satirsiz: true); // final için puan satırı yok
        Kur(ortam, donem, "PUANNULL", (40m, 70m), (60m, null));                 // final satırı var, puan null
        Kur(ortam, donem, "TAM101", (40m, 70m), (60m, 50m));                    // karşı örnek: 28 + 30 = 58,00

        var transkript = await ortam.TranskriptAsync();
        Assert.Null(TranskriptOrtami.Ders(transkript, donem, "SATIRYOK").AverageScore);
        Assert.Null(TranskriptOrtami.Ders(transkript, donem, "PUANNULL").AverageScore);
        Assert.Equal(58.00m, TranskriptOrtami.Ders(transkript, donem, "TAM101").AverageScore);
    }

    private static void Kur(TranskriptOrtami ortam, SIS.Domain.Entities.AcademicTerm donem, string kod,
        (decimal Agirlik, decimal? Puan) vize, (decimal Agirlik, decimal? Puan) final, bool satirsiz = false)
    {
        var ders = ortam.DersEkle(kod, 5);
        var kayit = ortam.KayitEkle(ders, donem, GradeCode.CC);
        ortam.PuanEkle(kayit, ortam.BilesenEkle(ders, donem, "Vize", vize.Agirlik), vize.Puan);
        var finalBileseni = ortam.BilesenEkle(ders, donem, "Final", final.Agirlik);
        if (!satirsiz)
            ortam.PuanEkle(kayit, finalBileseni, final.Puan);
    }
}
