using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.DersKaydi;

// C-07: AKTS sınırı (kural kodu ECTSCap), bugünkü davranış. Ortam ayar sağlayıcısı vermez: sınırlar üretim varsayılanlarıdır
// (AGNO < 2,00: 30, 2,00–2,49: 42, ≥ 2,50: 48) ve testlerde yalnız gözlenen sınır olarak geçer.
// Sınır doğrulama servisinde (ValidateRegistrationAsync) ve gönderimde (SubmitRegistrationAsync) uygulanır; plana ekleme uygulamaz.
public class C07_AktsSiniriTests
{
    private static Task<RegistrationResponseDto> Gonder(DersKaydiOrtami o, params CourseSection[] subeler) =>
        o.Kayit().SubmitRegistrationAsync(o.Ogrenci.Id, new RegistrationSubmitDto { CourseSectionIds = subeler.Select(s => s.Id).ToList() });

    private static async Task<(bool Basarili, string[] Kurallar)> Calistir(DersKaydiOrtami o, bool gonderim, params CourseSection[] subeler)
    {
        if (gonderim)
        {
            var y = await Gonder(o, subeler);
            return (y.Success, y.ValidationResult?.Errors.Select(e => e.Rule).ToArray() ?? []);
        }
        var s = await o.Dogrulama().ValidateRegistrationAsync(o.Ogrenci.Id, subeler.Select(x => x.Id).ToList());
        return (s.IsValid, s.Errors.Select(e => e.Rule).ToArray());
    }

    private static void AktsAyarla(DersKaydiOrtami o, CourseSection sube, int akts)
    {
        sube.Course.Ects = akts;
        o.Db.SaveChanges();
    }

    private static Dictionary<int, RegistrationStatus> Diger(DersKaydiOrtami o, int subeId) =>
        o.Db.CourseRegistrations.Where(r => r.CourseSectionId != subeId).ToDictionary(r => r.Id, r => r.Status);

    // Aynı kurulumda yalnız dersin AKTS'si değişir: sınır+1 tek nedenle (ECTSCap) reddedilir ve iz bırakmaz, sınırın kendisi kabul edilir.
    private static async Task SinirSina(DersKaydiOrtami o, CourseSection sube, int sinir, bool gonderim)
    {
        var onceki = Diger(o, sube.Id);

        AktsAyarla(o, sube, sinir + 1);
        var ret = await Calistir(o, gonderim, sube);
        Assert.False(ret.Basarili);
        Assert.Equal(new[] { "ECTSCap" }, ret.Kurallar);
        Assert.Equal(onceki, Diger(o, sube.Id));
        Assert.DoesNotContain(o.Db.CourseRegistrations, r => r.CourseSectionId == sube.Id);

        AktsAyarla(o, sube, sinir);
        var kabul = await Calistir(o, gonderim, sube);
        Assert.True(kabul.Basarili);
        Assert.Empty(kabul.Kurallar);
        Assert.Equal(onceki, Diger(o, sube.Id));
        if (gonderim)
            Assert.Equal(RegistrationStatus.PendingAdvisorApproval, o.Db.CourseRegistrations.Single(r => r.CourseSectionId == sube.Id).Status);
        else
            Assert.DoesNotContain(o.Db.CourseRegistrations, r => r.CourseSectionId == sube.Id);
    }

    // AGNO bantları: 1,99 ve altı 30'da, 2,00–2,49 42'de, 2,50 ve üstü 48'de kalır. AGNO 1,90 yalnız 1,70 altı kısıtından uzak durmak içindir.
    [Theory]
    [InlineData("1.90", 30, true)]
    [InlineData("1.99", 30, true)]
    [InlineData("2.00", 42, true)]
    [InlineData("2.49", 42, true)]
    [InlineData("2.50", 48, true)]
    [InlineData("1.90", 30, false)]
    [InlineData("1.99", 30, false)]
    [InlineData("2.00", 42, false)]
    [InlineData("2.49", 42, false)]
    [InlineData("2.50", 48, false)]
    public async Task C07_AgnoBantSinirlari_SinirKabulSinirArtiBirRet(string agno, int sinir, bool gonderim)
    {
        using var o = new DersKaydiOrtami(agno: decimal.Parse(agno, System.Globalization.CultureInfo.InvariantCulture));

        await SinirSina(o, o.Sube(o.DersEkle("XXX101")), sinir, gonderim);
    }

    // Mevcut Active, PendingAdvisorApproval ve Draft kayıtların AKTS'si toplama girer (25 + 5 = gözlenen sınır 30).
    [Theory]
    [InlineData(RegistrationStatus.Active)]
    [InlineData(RegistrationStatus.PendingAdvisorApproval)]
    [InlineData(RegistrationStatus.Draft)]
    public async Task C07_MevcutKayit_ToplamaGirer(RegistrationStatus durum)
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        o.KayitEkle(o.DersEkle("MMM101", 25), durum);

        await SinirSina(o, o.Sube(o.DersEkle("XXX101")), 5, gonderim: true);
    }

    // Diğer durumlar ve başka dönem toplama girmez: 25 AKTS'lik mevcut kayıtla 30 AKTS geçerli; kayıt etkin dönemde Active olunca ret.
    [Theory]
    [InlineData(RegistrationStatus.Completed, false)]
    [InlineData(RegistrationStatus.Withdrawn, false)]
    [InlineData(RegistrationStatus.Rejected, false)]
    [InlineData(RegistrationStatus.Dropped, false)]
    [InlineData(RegistrationStatus.Active, true)]
    public async Task C07_MevcutKayit_ToplamaGirmez(RegistrationStatus durum, bool baskaDonem)
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        var mevcut = o.KayitEkle(o.DersEkle("MMM101", 25), durum, baskaDonem ? o.GecmisDonem : null);
        var sube = o.Sube(o.DersEkle("XXX101", 30));

        var sayilmaz = await Calistir(o, false, sube);
        Assert.True(sayilmaz.Basarili);
        Assert.Empty(sayilmaz.Kurallar);

        mevcut.Status = RegistrationStatus.Active;
        mevcut.CourseSection.Term = o.EtkinDonem;
        o.Db.SaveChanges();
        var sayilir = await Calistir(o, false, sube);
        Assert.False(sayilir.Basarili);
        Assert.Equal(new[] { "ECTSCap" }, sayilir.Kurallar);
    }

    // Mevcut kayıtta AKTS anlık görüntüsü varsa dersin katalog değerinin yerine geçer (ders 5, anlık görüntü 25).
    [Fact]
    public async Task C07_MevcutKayit_AnlikGoruntuKatalogDegerinin_YerineGecer()
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        o.KayitEkle(o.DersEkle("MMM101", 5), RegistrationStatus.Active).EctsSnapshot = 25;
        o.Db.SaveChanges();

        await SinirSina(o, o.Sube(o.DersEkle("XXX101")), 5, gonderim: false);
    }

    // Gönderilen şubenin kendi taslağı mevcut sayılmaz (çift sayım yok): 30 AKTS'lik taslak tek başına geçer, başka 1 AKTS eklenince ret.
    [Fact]
    public async Task C07_GonderilenSubeninTaslagi_CiftSayilmaz()
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        var sube = o.Sube(o.DersEkle("XXX101", 30));
        Assert.True((await o.Kayit().AddToPlanAsync(o.Ogrenci.Id, sube.Id)).Success);
        var diger = o.KayitEkle(o.DersEkle("MMM101", 1), RegistrationStatus.Active);

        var ret = await Gonder(o, sube);
        Assert.False(ret.Success);
        Assert.Equal("ECTSCap", Assert.Single(ret.ValidationResult!.Errors).Rule);
        Assert.Equal(RegistrationStatus.Draft, o.Db.CourseRegistrations.Single(r => r.CourseSectionId == sube.Id).Status);

        diger.Status = RegistrationStatus.Completed;
        o.Db.SaveChanges();
        Assert.True((await Gonder(o, sube)).Success);
        Assert.Equal(RegistrationStatus.PendingAdvisorApproval, o.Db.CourseRegistrations.Single(r => r.CourseSectionId == sube.Id).Status);
    }

    // Müfredat alanı doluysa varsayılanın yerine geçer (hem büyük hem küçük değerle); boşalınca varsayılan sınır döner. Her alan kendi bandında.
    [Theory]
    [InlineData("1.90", 0, 36, 30)]
    [InlineData("1.90", 0, 25, 30)]
    [InlineData("2.20", 1, 44, 42)]
    [InlineData("2.20", 1, 40, 42)]
    [InlineData("2.60", 2, 50, 48)]
    [InlineData("2.60", 2, 45, 48)]
    public async Task C07_MufredatAlani_VarsayilaninYerineGecer(string agno, int bant, int mufredatSiniri, int varsayilanSinir)
    {
        using var o = new DersKaydiOrtami(agno: decimal.Parse(agno, System.Globalization.CultureInfo.InvariantCulture));
        var sube = o.Sube(o.DersEkle("XXX101"));

        o.MufredatAktsSiniriAyarla(bant == 0 ? mufredatSiniri : null, bant == 1 ? mufredatSiniri : null, bant == 2 ? mufredatSiniri : null);
        await SinirSina(o, sube, mufredatSiniri, gonderim: false);

        o.MufredatAktsSiniriAyarla();
        await SinirSina(o, sube, varsayilanSinir, gonderim: false);
    }

    // Kredisiz açma nedeni olan talep edilen ders toplamdan düşer (30 + 10 kredisiz geçer); neden kalkınca ret.
    [Fact]
    public async Task C07_KredisizTalepEdilenDers_ToplamdanDuser()
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        var sube = o.Sube(o.DersEkle("XXX101", 30));
        var kredisizDers = o.DersEkle("KKK101", 10);
        var kredisiz = o.Sube(kredisizDers);
        var mufredatDersi = o.KredisizMufredataEkle(kredisizDers);

        var sayilmaz = await Calistir(o, false, sube, kredisiz);
        Assert.True(sayilmaz.Basarili);
        Assert.Empty(sayilmaz.Kurallar);

        mufredatDersi.OpeningReason = null;
        o.Db.SaveChanges();
        var sayilir = await Calistir(o, false, sube, kredisiz);
        Assert.False(sayilir.Basarili);
        Assert.Equal(new[] { "ECTSCap" }, sayilir.Kurallar);
    }

    // Kredisiz açma nedeni anlık görüntüsü olan mevcut kayıt toplamdan düşer (25 kredisiz + 30 geçer); anlık görüntü kalkınca ret.
    [Fact]
    public async Task C07_KredisizMevcutKayit_ToplamdanDuser()
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        var mevcut = o.KayitEkle(o.DersEkle("MMM101", 25), RegistrationStatus.Active);
        mevcut.OpeningReasonSnapshot = o.KredisizNeden();
        o.Db.SaveChanges();
        var sube = o.Sube(o.DersEkle("XXX101", 30));

        var sayilmaz = await Calistir(o, false, sube);
        Assert.True(sayilmaz.Basarili);
        Assert.Empty(sayilmaz.Kurallar);

        mevcut.OpeningReasonSnapshot = null;
        o.Db.SaveChanges();
        var sayilir = await Calistir(o, false, sube);
        Assert.False(sayilir.Basarili);
        Assert.Equal(new[] { "ECTSCap" }, sayilir.Kurallar);
    }

    // Plana ekleme sınırı uygulamaz: 31 AKTS taslak olarak eklenir; sınır gönderimde devreye girer, taslak değişmez.
    // AKTS sınıra çekilince aynı plan gönderilir.
    [Fact]
    public async Task C07_PlanaEkleme_SinirUygulanmaz_GonderimdeUygulanir()
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        var sube = o.Sube(o.DersEkle("XXX101", 31));

        var plan = await o.Kayit().AddToPlanAsync(o.Ogrenci.Id, sube.Id);

        Assert.True(plan.Success);
        Assert.Equal(RegistrationStatus.Draft, o.Db.CourseRegistrations.Single(r => r.CourseSectionId == sube.Id).Status);

        var gonderim = await Gonder(o, sube);

        Assert.False(gonderim.Success);
        Assert.Equal("ECTSCap", Assert.Single(gonderim.ValidationResult!.Errors).Rule);
        Assert.Equal(RegistrationStatus.Draft, o.Db.CourseRegistrations.Single(r => r.CourseSectionId == sube.Id).Status);

        AktsAyarla(o, sube, 30);
        Assert.True((await Gonder(o, sube)).Success);
        Assert.Equal(RegistrationStatus.PendingAdvisorApproval, o.Db.CourseRegistrations.Single(r => r.CourseSectionId == sube.Id).Status);
    }
}
