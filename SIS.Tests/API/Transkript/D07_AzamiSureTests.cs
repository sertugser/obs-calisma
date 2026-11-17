using Microsoft.EntityFrameworkCore;
using Moq;
using SIS.Application.Admin.DTOs;
using SIS.Application.Identity.Services;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Scope;

namespace SIS.Tests.API.Transkript;

// D-07: program oluşturma ve güncellemede azami öğrenim süresi için doğrulama yok; gelen değer olduğu gibi yazılıyor.
public class D07_AzamiSureTests
{
    // 14 geçerli bir değer (karşı örnek): aynı yoldan yazılıyor.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    [InlineData(14)]
    public async Task D07_ProgramOlusturVeGuncelle_AzamiSureOlduguGibiYaziliyor(int deger)
    {
        using var ortam = new TranskriptOrtami();
        var servis = new AdminAcademicStructureService(ortam.Db, Mock.Of<IScopeGuard>(), Mock.Of<IScopeResolver>());

        var olusan = await servis.CreateProgramAsync(new CreateProgramDto(
            "Yeni Program", "YP" + (deger + 10), null, ortam.Bolum.Id, "Bachelor", "Semester", deger, 240));
        Assert.Equal(deger, olusan.MaxStudyDurationSemesters);
        Assert.Equal(deger, (await ortam.Db.AcademicPrograms.AsNoTracking().SingleAsync(p => p.Id == olusan.Id)).MaxStudyDurationSemesters);

        // Ara güncelleme: 0 satırında da son güncellemenin değeri gerçekten yazdığını ayırt eder (1 → 0).
        await servis.UpdateProgramAsync(olusan.Id, new UpdateProgramDto(
            "Yeni Program", olusan.Code, null, ortam.Bolum.Id, "Bachelor", "Semester", deger * 2 + 1, 240));
        Assert.Equal(deger * 2 + 1, (await ortam.Db.AcademicPrograms.AsNoTracking().SingleAsync(p => p.Id == olusan.Id)).MaxStudyDurationSemesters);

        var guncel = await servis.UpdateProgramAsync(olusan.Id, new UpdateProgramDto(
            "Yeni Program", olusan.Code, null, ortam.Bolum.Id, "Bachelor", "Semester", deger * 2, 240));
        Assert.Equal(deger * 2, guncel.MaxStudyDurationSemesters);
        Assert.Equal(deger * 2, (await ortam.Db.AcademicPrograms.AsNoTracking().SingleAsync(p => p.Id == olusan.Id)).MaxStudyDurationSemesters);
    }
}
