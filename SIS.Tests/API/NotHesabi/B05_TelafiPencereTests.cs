using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.NotHesabi;

// B-05: Şube sonuçlandırılmış ve not giriş penceresi kapalı. Büt ve mazeret sonuçlandırması
// yalnız dönem iptaline bakıyor; normal sonuçlandırma aynı pencereyle reddediliyor.
public class B05_TelafiPencereTests
{
    // Bilinen hata H6: bütünleme sonuçlandırması not giriş penceresini denetlemiyor.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task B05_Butunleme_PencereKapaliyken_KabulEdiliyor_FFdenCB()
    {
        using var ortam = Kur(y => { y.AutoTransferFailedToMakeup = true; y.MakeupReplacesFinal = true; });
        var (vize, final) = (ortam.BilesenEkle("Vize", 40m, GradeComponentType.Midterm, 1), ortam.BilesenEkle("Final", 60m, GradeComponentType.Final, 2));
        var (_, _, kayit) = ortam.OgrenciEkle("01");
        ortam.PuanEkle(kayit, vize, 40m);
        ortam.PuanEkle(kayit, final, 30m); // 16 + 18 = 34 → FF

        var kapali = await SonuclandirVeKapat(ortam, kayit, final, GradeCode.FF); // büt 80: 16 + 48 = 64

        Assert.IsType<OkObjectResult>(await kapali.FinalizeButunleme(ortam.Sube.Id));
        Assert.Equal(GradeCode.CB, (await ortam.Db.CourseRegistrations.FindAsync(kayit.Id))!.Grade);
    }

    // Bilinen hata H6: mazeret sonuçlandırması not giriş penceresini denetlemiyor.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task B05_Mazeret_PencereKapaliyken_KabulEdiliyor_IdenCB()
    {
        using var ortam = Kur(y => y.AutoTransferExcusedToMakeup = true);
        var (vize, final) = (ortam.BilesenEkle("Vize", 40m, GradeComponentType.Midterm, 1), ortam.BilesenEkle("Final", 60m, GradeComponentType.Final, 2));
        var (_, _, kayit) = ortam.OgrenciEkle("01");
        ortam.PuanEkle(kayit, vize, 40m);
        ortam.PuanEkle(kayit, final, null, ExamAttendance.Excused);

        var kapali = await SonuclandirVeKapat(ortam, kayit, final, GradeCode.I); // telafi 80: 16 + 48 = 64

        Assert.IsType<OkObjectResult>(await kapali.FinalizeMazeret(ortam.Sube.Id));
        Assert.Equal(GradeCode.CB, (await ortam.Db.CourseRegistrations.FindAsync(kayit.Id))!.Grade);
    }

    // Üç sonuçlandırma ucunun bugünkü izin anahtarı.
    [Theory]
    [InlineData(nameof(GradeController.Finalize), "menu.not-girisi.finalize")]
    [InlineData(nameof(GradeController.FinalizeButunleme), "menu.not-girisi.edit")]
    [InlineData(nameof(GradeController.FinalizeMazeret), "menu.not-girisi.edit")]
    public void B05_SonuclandirmaUclarininIzinAnahtari(string uc, string izin)
    {
        var oznitelik = typeof(GradeController).GetMethod(uc)!.GetCustomAttribute<RequirePermissionAttribute>()!;
        var anahtarlar = typeof(RequirePermissionAttribute)
            .GetField("_permissionKeys", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(oznitelik);

        Assert.Equal([izin], (string[])anahtarlar!);
    }

    private static NotHesabiOrtami Kur(Action<ExamRegulation> yonerge)
    {
        var ortam = new NotHesabiOrtami();
        ortam.HarfOlcutuEkle();
        ortam.YonergeEkle(yonerge);
        return ortam;
    }

    // Açık pencereyle sonuçlandırır, final satırına telafi puanı 80 yazar, kapalı pencereli controller döner.
    // Karşı örnek: aynı kapalı pencereyle normal sonuçlandırma pencere mesajıyla reddediliyor.
    private static async Task<GradeController> SonuclandirVeKapat(NotHesabiOrtami ortam, CourseRegistration kayit, GradeComponent final, GradeCode ilkHarf)
    {
        Assert.IsType<OkObjectResult>(await ortam.NotController().Finalize(ortam.Sube.Id));
        Assert.Equal(ilkHarf, (await ortam.Db.CourseRegistrations.FindAsync(kayit.Id))!.Grade);
        ortam.Db.StudentGrades.Single(g => g.CourseRegistrationId == kayit.Id && g.GradeComponentId == final.Id).MakeupScore = 80m;
        ortam.Db.SaveChanges();

        var kapali = ortam.NotController(new SaplamaPencereServisi(new Dictionary<string, WebModuleAccessResult>
        {
            ["instructor-grade-entry"] = SaplamaPencereServisi.Kapali(WebModuleControlModes.Closed)
        }));
        var red = Assert.IsType<BadRequestObjectResult>(await kapali.Finalize(ortam.Sube.Id));
        Assert.Equal("Pencere kapalı.", NotHesabiOrtami.Oku<string>(red.Value!, "error"));
        return kapali;
    }
}
