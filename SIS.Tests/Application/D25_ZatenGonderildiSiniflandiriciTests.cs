using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Application;

// D25: gönderimde yakalanan veritabanı hatası yalnız öğrenci-şube tekil dizininin ihlaliyse "zaten gönderildi" sayılır.
public class D25_ZatenGonderildiSiniflandiriciTests
{
    private const string Dizin = "IX_CourseRegistrations_StudentId_CourseSectionId";

    [Fact]
    public void D25_OgrenciSubeTekilDizinIhlali_Eslesir() =>
        Assert.True(CourseRegistrationService.IsAlreadySubmittedConflict(Hata("23505", Dizin)));

    // Karşı örnekler: aynı kod başka kısıtta, aynı kısıt adı başka kodda, iç hata yok.
    [Theory]
    [InlineData("23505", "IX_Baska_Dizin")]
    [InlineData("23503", Dizin)]
    [InlineData("23505", null)]
    public void D25_KarsiOrnek_BaskaKisitYaDaBaskaKod_Eslesmez(string kod, string? kisit) =>
        Assert.False(CourseRegistrationService.IsAlreadySubmittedConflict(Hata(kod, kisit)));

    [Fact]
    public void D25_KarsiOrnek_IcHataYok_Eslesmez() =>
        Assert.False(CourseRegistrationService.IsAlreadySubmittedConflict(new DbUpdateException("hata")));

    private static DbUpdateException Hata(string kod, string? kisit) =>
        new("hata", new PostgresException("hata", "ERROR", "ERROR", kod, constraintName: kisit));
}
