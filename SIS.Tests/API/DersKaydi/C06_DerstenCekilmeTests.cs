using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;
using SIS.Tests.TestDoubles;

namespace SIS.Tests.API.DersKaydi;

// C-06: dersten çekilme (WithdrawCourseAsync) kolları, bugünkü davranış. Ortam ayar sağlayıcısı vermez:
// azami çekilme sayıları (6 / 3) üretim varsayılanlarıdır ve testte yalnız gözlenen sınır olarak geçer.
// Her ret testi aynı kurulumda yalnız ölçülen koşulu düzeltip çekilmenin kabul edildiğini de gösterir.
// Kurum saat dilimi açıkça verilir (Europe/Istanbul, +03:00): çekilme son günü yerel gün sonunda kapanır.
public class C06_DerstenCekilmeTests
{
    private static Task<RegistrationResponseDto> Cek(DersKaydiOrtami o, CourseRegistration k) =>
        new CourseRegistrationService(o.Db, o.Dogrulama(), o.Saat, institutionTimeZone: new SabitKurumSaatDilimi("Europe/Istanbul"))
            .WithdrawCourseAsync(o.Ogrenci.Id, k.Id);

    private static string TekRet(RegistrationResponseDto y)
    {
        Assert.False(y.Success);
        return Assert.Single(y.ValidationResult!.Errors).Rule;
    }

    // Ret sonrası kalıcı durum: kayıt aynı durumda, not yok, sayaçlar ve kontenjan sayısı aynı.
    private static void Degismedi(DersKaydiOrtami o, int kayitId, RegistrationStatus durum, int ogrenciSayaci = 0, int cekilmeSayaci = 0)
    {
        var k = o.Db.CourseRegistrations.AsNoTracking().Single(r => r.Id == kayitId);
        Assert.Equal(durum, k.Status);
        Assert.Null(k.Grade);
        Assert.Equal(cekilmeSayaci, k.CourseWithdrawalCount);
        Assert.Equal(ogrenciSayaci, o.Db.Students.AsNoTracking().Single().WithdrawalCount);
        Assert.Equal(1, o.Db.CourseSections.AsNoTracking().Single(s => s.Id == k.CourseSectionId).EnrolledCount);
    }

    // Kabul sonrası kalıcı durum: Withdrawn, W notu, ders ve öğrenci sayaçları 1 artmış, kontenjan sayısı düşmüş.
    private static void Cekildi(DersKaydiOrtami o, RegistrationResponseDto y, int kayitId, int ogrenciSayaci = 0)
    {
        Assert.True(y.Success);
        Assert.Equal("Withdrawn", Assert.Single(y.Registrations!).Status);

        var k = o.Db.CourseRegistrations.AsNoTracking().Single(r => r.Id == kayitId);
        Assert.Equal(RegistrationStatus.Withdrawn, k.Status);
        Assert.Equal(GradeCode.W, k.Grade);
        Assert.Equal(1, k.CourseWithdrawalCount);
        Assert.Equal(ogrenciSayaci + 1, o.Db.Students.AsNoTracking().Single().WithdrawalCount);
        Assert.Equal(0, o.Db.CourseSections.AsNoTracking().Single(s => s.Id == k.CourseSectionId).EnrolledCount);
    }

    private static (CourseRegistration a, CourseRegistration b) IkiDers(DersKaydiOrtami o) =>
        (o.AktifKayitEkle(o.DersEkle("AAA101")), o.AktifKayitEkle(o.DersEkle("BBB101")));

    [Fact]
    public async Task C06_Cekilme_KosullarSaglaninca_KabulEdilirVeKalicidir()
    {
        using var o = new DersKaydiOrtami();
        var (a, b) = IkiDers(o);

        var y = await Cek(o, a);

        Cekildi(o, y, a.Id);
        Assert.Equal(RegistrationStatus.Active, o.Db.CourseRegistrations.Single(r => r.Id == b.Id).Status);
    }

    // Kol 1: çekilme penceresi (TermStart ≤ an < son günün yerel bitişi). Son gün 10 Mayıs, İstanbul'da
    // 11 Mayıs 00:00 yerel = 10 Mayıs 21:00Z'de kapanır. Karşı örnek: saat pencereye alınınca kabul.
    [Theory]
    [InlineData("2025-02-14T23:59:59", false)]
    [InlineData("2025-02-15T00:00:00", true)]
    [InlineData("2025-05-10T00:00:00", true)]
    [InlineData("2025-05-10T00:00:01", true)]
    [InlineData("2025-05-10T20:59:59", true)]
    [InlineData("2025-05-10T21:00:00", false)]
    [InlineData("2025-05-11T12:00:00", false)]
    public async Task C06_Cekilme_PencereSinirlari_TermStatus(string an, bool kabul)
    {
        using var o = new DersKaydiOrtami();
        var (a, _) = IkiDers(o);
        o.Saat.UtcNow = DateTime.Parse(an, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

        var y = await Cek(o, a);

        if (kabul)
        {
            Cekildi(o, y, a.Id);
            return;
        }
        Assert.Equal("TermStatus", TekRet(y));
        Degismedi(o, a.Id, RegistrationStatus.Active);
        o.Saat.UtcNow = new DateTime(2025, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        Cekildi(o, await Cek(o, a), a.Id);
    }

    [Fact]
    public async Task C06_Cekilme_DonemPasifse_TermStatusIleReddedilir()
    {
        using var o = new DersKaydiOrtami();
        var (a, _) = IkiDers(o);
        o.EtkinDonem.IsActivated = false;
        o.Db.SaveChanges();

        Assert.Equal("TermStatus", TekRet(await Cek(o, a)));
        Degismedi(o, a.Id, RegistrationStatus.Active);

        o.EtkinDonem.IsActivated = true;
        o.Db.SaveChanges();
        Cekildi(o, await Cek(o, a), a.Id);
    }

    // Kol 2: yalnız Active kayıttan çekilinir.
    [Fact]
    public async Task C06_Cekilme_KayitActiveDegilse_WithdrawStatusIleReddedilir()
    {
        using var o = new DersKaydiOrtami();
        var (a, _) = IkiDers(o);
        a.Status = RegistrationStatus.Draft;
        o.Db.SaveChanges();

        Assert.Equal("WithdrawStatus", TekRet(await Cek(o, a)));
        Degismedi(o, a.Id, RegistrationStatus.Draft);

        a.Status = RegistrationStatus.Active;
        o.Db.SaveChanges();
        Cekildi(o, await Cek(o, a), a.Id);
    }

    // Kol 3: dönemdeki tek aktif dersten çekilinemez.
    [Fact]
    public async Task C06_Cekilme_TekAktifDers_LastCourseIleReddedilir()
    {
        using var o = new DersKaydiOrtami();
        var a = o.AktifKayitEkle(o.DersEkle("AAA101"));

        Assert.Equal("LastCourse", TekRet(await Cek(o, a)));
        Degismedi(o, a.Id, RegistrationStatus.Active);

        o.AktifKayitEkle(o.DersEkle("BBB101"));
        Cekildi(o, await Cek(o, a), a.Id);
    }

    // Kol 3 sayımı yalnız aynı dönemdeki Active kayıtları görür: Draft ikinci kayıt ya da başka dönemdeki Active kayıt saymaz.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task C06_Cekilme_SonDersSayimi_DraftVeBaskaDonemKayitiSaymaz_LastCourseIleReddedilir(bool baskaDonem)
    {
        using var o = new DersKaydiOrtami();
        var a = o.AktifKayitEkle(o.DersEkle("AAA101"));
        var diger = baskaDonem
            ? o.KayitEkle(o.DersEkle("BBB101"), RegistrationStatus.Active, o.GecmisDonem)
            : o.KayitEkle(o.DersEkle("BBB101"), RegistrationStatus.Draft);

        Assert.Equal("LastCourse", TekRet(await Cek(o, a)));
        Degismedi(o, a.Id, RegistrationStatus.Active);

        if (baskaDonem)
            diger.CourseSection.Term = o.EtkinDonem;
        else
            diger.Status = RegistrationStatus.Active;
        o.Db.SaveChanges();
        Cekildi(o, await Cek(o, a), a.Id);
    }

    // Kol 4: aynı dersten ikinci çekilme.
    [Fact]
    public async Task C06_Cekilme_DahaOnceCekilinmisDers_DuplicateWithdrawalIleReddedilir()
    {
        using var o = new DersKaydiOrtami();
        var (a, _) = IkiDers(o);
        a.CourseWithdrawalCount = 1;
        o.Db.SaveChanges();

        Assert.Equal("DuplicateWithdrawal", TekRet(await Cek(o, a)));
        Degismedi(o, a.Id, RegistrationStatus.Active, cekilmeSayaci: 1);

        a.CourseWithdrawalCount = 0;
        o.Db.SaveChanges();
        Cekildi(o, await Cek(o, a), a.Id);
    }

    // Kol 5: ilk iki yarıyıl (CompletedSemesters < 2), yalnız Bachelor ve Associate. Karşı örnek: 2 yarıyıl.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task C06_Cekilme_IlkIkiYariyil_FirstTwoSemestersIleReddedilir(int tamamlanan)
    {
        using var o = new DersKaydiOrtami(tamamlananDonem: tamamlanan);
        var (a, _) = IkiDers(o);

        Assert.Equal("FirstTwoSemesters", TekRet(await Cek(o, a)));
        Degismedi(o, a.Id, RegistrationStatus.Active);

        o.Ogrenci.CompletedSemesters = 2;
        o.Db.SaveChanges();
        Cekildi(o, await Cek(o, a), a.Id);
    }

    [Theory]
    [InlineData(DegreeLevel.Associate, false)]
    [InlineData(DegreeLevel.Bachelor, false)]
    [InlineData(DegreeLevel.Master, true)]
    [InlineData(DegreeLevel.Doctorate, true)]
    public async Task C06_Cekilme_IlkYariyilKurali_YalnizOnlisansVeLisansa(DegreeLevel duzey, bool kabul)
    {
        using var o = new DersKaydiOrtami(tamamlananDonem: 1);
        o.ProgramDuzeyiAyarla(duzey);
        var (a, _) = IkiDers(o);

        var y = await Cek(o, a);

        if (kabul)
            Cekildi(o, y, a.Id);
        else
        {
            Assert.Equal("FirstTwoSemesters", TekRet(y));
            Degismedi(o, a.Id, RegistrationStatus.Active);
        }
    }

    // Kol 6: FF/NA/U ile kalmış ve sonradan geçmemiş ders. Karşı örnek: sonradan geçilmiş.
    [Theory]
    [InlineData(GradeCode.FF)]
    [InlineData(GradeCode.NA)]
    [InlineData(GradeCode.U)]
    public async Task C06_Cekilme_AlttanDers_MandatoryRetakeIleReddedilir(GradeCode eskiNot)
    {
        using var o = new DersKaydiOrtami();
        var ders = o.DersEkle("AAA101");
        o.NotEkle(ders, eskiNot, o.OncekiDonem);
        var a = o.AktifKayitEkle(ders);
        o.AktifKayitEkle(o.DersEkle("BBB101"));

        Assert.Equal("MandatoryRetake", TekRet(await Cek(o, a)));
        Degismedi(o, a.Id, RegistrationStatus.Active);

        o.NotEkle(ders, GradeCode.CC, o.GecmisDonem);
        Cekildi(o, await Cek(o, a), a.Id);
    }

    // Kol 6: başarısız nottan sonraki W ya da I kaydı dersi "sonradan geçilmiş" yapmaz, yasak sürer.
    [Theory]
    [InlineData(GradeCode.W)]
    [InlineData(GradeCode.I)]
    public async Task C06_Cekilme_AlttanDers_SonrakiWVeyaIGecmisSayilmaz_MandatoryRetakeSurer(GradeCode sonrakiNot)
    {
        using var o = new DersKaydiOrtami();
        var ders = o.DersEkle("AAA101");
        o.NotEkle(ders, GradeCode.FF, o.OncekiDonem);
        var sonraki = o.NotEkle(ders, sonrakiNot, o.GecmisDonem);
        var a = o.AktifKayitEkle(ders);
        o.AktifKayitEkle(o.DersEkle("BBB101"));

        Assert.Equal("MandatoryRetake", TekRet(await Cek(o, a)));
        Degismedi(o, a.Id, RegistrationStatus.Active);

        sonraki.Grade = GradeCode.CC;
        o.Db.SaveChanges();
        Cekildi(o, await Cek(o, a), a.Id);
    }

    // Kol 7: azami çekilme sayısı (Associate 3, diğerleri 6; üretim varsayılanı). Aynı düzeyde bir eksik sayı kabul edilir.
    [Theory]
    [InlineData(DegreeLevel.Bachelor, 5, true)]
    [InlineData(DegreeLevel.Bachelor, 6, false)]
    [InlineData(DegreeLevel.Associate, 2, true)]
    [InlineData(DegreeLevel.Associate, 3, false)]
    [InlineData(DegreeLevel.Master, 5, true)]
    [InlineData(DegreeLevel.Master, 6, false)]
    public async Task C06_Cekilme_AzamiSayi_MaxWithdrawalsSiniri(DegreeLevel duzey, int onceki, bool kabul)
    {
        using var o = new DersKaydiOrtami();
        o.ProgramDuzeyiAyarla(duzey);
        o.Ogrenci.WithdrawalCount = onceki;
        o.Db.SaveChanges();
        var (a, _) = IkiDers(o);

        var y = await Cek(o, a);

        if (kabul)
            Cekildi(o, y, a.Id, onceki);
        else
        {
            Assert.Equal("MaxWithdrawals", TekRet(y));
            Degismedi(o, a.Id, RegistrationStatus.Active, onceki);
        }
    }

    // Kol 3 ve 4 aynı anda bozulunca ikisi birlikte döner (sıra: LastCourse, DuplicateWithdrawal); biri düzelince yalnız öteki kalır.
    [Fact]
    public async Task C06_Cekilme_IkiKosulBirlikteBozuksa_IkiHataBirlikteDoner()
    {
        using var o = new DersKaydiOrtami();
        var a = o.AktifKayitEkle(o.DersEkle("AAA101"));
        a.CourseWithdrawalCount = 1;
        o.Db.SaveChanges();

        var y = await Cek(o, a);

        Assert.False(y.Success);
        Assert.Equal(new[] { "LastCourse", "DuplicateWithdrawal" }, y.ValidationResult!.Errors.Select(e => e.Rule));
        Degismedi(o, a.Id, RegistrationStatus.Active, cekilmeSayaci: 1);

        o.AktifKayitEkle(o.DersEkle("BBB101"));
        Assert.Equal("DuplicateWithdrawal", TekRet(await Cek(o, a)));
        a.CourseWithdrawalCount = 0;
        o.Db.SaveChanges();
        Cekildi(o, await Cek(o, a), a.Id);
    }

    // Dönemdeki yönetici istisnası (WithdrawalOverride; gözlenen davranış): true tarihe bakmadan açar, false pencere içinde bile kapatır,
    // boşsa tarih geçerlidir. Karşı örnek: istisna boşken sonuç tarihe göre tersine döner.
    [Theory]
    [InlineData(true, "2025-05-11T12:00:00", true)]
    [InlineData(false, "2025-03-10T12:00:00", false)]
    public async Task C06_Cekilme_PencereIstisnasi_TarihinYerineGecer(bool istisna, string an, bool kabul)
    {
        using var o = new DersKaydiOrtami();
        var (a, _) = IkiDers(o);
        o.Saat.UtcNow = DateTime.Parse(an, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

        void Ayarla(bool? deger)
        {
            o.EtkinDonem.AcademicCalendar!.WithdrawalOverride = deger;
            o.Db.SaveChanges();
        }

        Ayarla(kabul ? null : istisna);
        Assert.Equal("TermStatus", TekRet(await Cek(o, a)));
        Degismedi(o, a.Id, RegistrationStatus.Active);

        Ayarla(kabul ? istisna : null);
        Cekildi(o, await Cek(o, a), a.Id);
    }

    // İstisna açık olsa da etkin olmayan dönemde çekilme kapalıdır.
    [Fact]
    public async Task C06_Cekilme_PencereIstisnasiAcikAmaDonemPasif_TermStatusIleReddedilir()
    {
        using var o = new DersKaydiOrtami();
        var (a, _) = IkiDers(o);
        o.EtkinDonem.AcademicCalendar!.WithdrawalOverride = true;
        o.EtkinDonem.IsActivated = false;
        o.Db.SaveChanges();

        Assert.Equal("TermStatus", TekRet(await Cek(o, a)));
        Degismedi(o, a.Id, RegistrationStatus.Active);

        o.EtkinDonem.IsActivated = true;
        o.Db.SaveChanges();
        Cekildi(o, await Cek(o, a), a.Id);
    }
}
