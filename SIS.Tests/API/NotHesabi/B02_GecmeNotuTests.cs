using Microsoft.AspNetCore.Mvc;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.NotHesabi;

// B-02: Sınav yönergesindeki geçme notu; 0 "kapalı" demek. 50 kolu (45 < 50 → FF) mevcut
// GradeFinalizeRegulationTests.Finalize_AssignsFF_WhenAverageBelowPassingGrade ile zaten sabit.
public class B02_GecmeNotuTests
{
    private static readonly int[] HarfEsikleri = [91, 81, 71, 61, 55, 50, 45];
    private static readonly int[] TSkorEsikleri = [70, 63, 57, 50, 45, 40, 35];

    [Fact]
    public void B02_YonergeVarliginVarsayilanlari()
    {
        var y = new ExamRegulation();

        Assert.Equal(0m, y.PassingGrade);
        Assert.Equal((1, 1), (y.MidtermCount, y.FinalCount));
        Assert.Equal((40m, 60m, 40m, 60m), (y.MinMidtermWeightPercent, y.MaxMidtermWeightPercent, y.MinFinalWeightPercent, y.MaxFinalWeightPercent));
        Assert.Equal(0m, y.FinalExamFloor);
        Assert.Equal(GradeCode.FF, y.BelowFloorGrade);
        Assert.Equal(HarfEsikleri, new[] { y.AAMin, y.BAMin, y.BBMin, y.CBMin, y.CCMin, y.DCMin, y.DDMin });
        Assert.Equal(TSkorEsikleri, new[] { y.TAAMin, y.TBAMin, y.TBBMin, y.TCBMin, y.TCCMin, y.TDCMin, y.TDDMin });
        Assert.Equal(10, y.RelativeMinStudentCount);
    }

    [Fact]
    public void B02_YonergeIstegininVarsayilanParametreleri()
    {
        var r = Istek(gecmeNotu: 0m, programId: 1);

        Assert.Equal(HarfEsikleri, new[] { r.AAMin, r.BAMin, r.BBMin, r.CBMin, r.CCMin, r.DCMin, r.DDMin });
        Assert.Equal(TSkorEsikleri, new[] { r.TAAMin, r.TBAMin, r.TBBMin, r.TCBMin, r.TCCMin, r.TDCMin, r.TDDMin });
        Assert.Equal(GradeCode.FF, r.BelowFloorGrade);
    }

    [Fact]
    public async Task B02_GecmeNotuSifir_KontrolYapilmiyor_Ortalama45DD()
    {
        using var ortam = new NotHesabiOrtami();
        var vize = ortam.BilesenEkle("Vize", 40m, GradeComponentType.Midterm, 1);
        var final = ortam.BilesenEkle("Final", 60m, GradeComponentType.Final, 2);
        ortam.HarfOlcutuEkle();
        ortam.YonergeEkle(y => { y.PassingGrade = 0m; y.FinalExamFloor = 0m; });
        var (_, _, kayit) = ortam.OgrenciEkle("01");
        ortam.PuanEkle(kayit, vize, 40m);
        ortam.PuanEkle(kayit, final, 48m); // 16 + 28,8 = 44,8 → 45

        Assert.IsType<OkObjectResult>(await ortam.NotController().Finalize(ortam.Sube.Id));
        Assert.Equal(GradeCode.DD, (await ortam.Db.CourseRegistrations.FindAsync(kayit.Id))!.Grade);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    [InlineData(0, true)]
    [InlineData(100, true)]
    public async Task B02_GecmeNotu_0ile100DisindaReddediliyor(int gecmeNotu, bool kabul)
    {
        using var ortam = new NotHesabiOrtami();

        var sonuc = await YonergeController(ortam).Create(Istek(gecmeNotu, ortam.Program.Id), default);

        Assert.IsType(kabul ? typeof(OkObjectResult) : typeof(BadRequestObjectResult), sonuc);
    }

    [Fact]
    public async Task B02_Kopyalama_GecmeNotunuTasiyor()
    {
        using var ortam = new NotHesabiOrtami();
        var controller = YonergeController(ortam);
        var kaynak = (ExamRegulationDto)((OkObjectResult)await controller.Create(Istek(37m, ortam.Program.Id), default)).Value!;

        var kopya = Assert.IsType<OkObjectResult>(await controller.Clone(kaynak.Id, new CloneExamRegulationRequest(null, 2025), default));

        var dto = Assert.IsType<ExamRegulationDto>(kopya.Value);
        Assert.Equal((2025, 37m), (dto.Year, dto.PassingGrade));
    }

    private static ExamRegulationController YonergeController(NotHesabiOrtami ortam) =>
        new(ortam.Db, new NullSystemConfigProvider()) { ControllerContext = NotHesabiOrtami.KullaniciBaglami(1) };

    private static SaveExamRegulationRequest Istek(decimal gecmeNotu, int programId) => new(
        ProgramId: programId, DegreeLevel: DegreeLevel.Bachelor, TermType: TermType.Fall, Year: 2024,
        IsActive: true, Name: "Deneme Yönergesi", MidtermCount: 1, FinalCount: 1,
        MinMidtermWeightPercent: 40, MaxMidtermWeightPercent: 60, MinFinalWeightPercent: 40, MaxFinalWeightPercent: 60,
        PassingGrade: gecmeNotu, RawScoreFloor1: 0, RawScoreFloor2: 0, FinalExamFloor: 0);
}
