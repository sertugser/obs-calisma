using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Enums;

namespace SIS.Tests.Postgres.DersKaydi;

// D25: öğrencinin gönderilmiş (onay bekleyen ya da kesinleşmiş) kaydı olan şubeye yeniden gönderimi 500 değil, AlreadySubmitted retidir.
// Kapanmış kayıt (reddedilmiş, bırakılmış, çekilmiş) yeniden gönderimde aynı satırla yeniden kullanılır.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class D25_ZatenGonderildiTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const RegistrationStatus Bekliyor = RegistrationStatus.PendingAdvisorApproval;

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(RegistrationStatus.PendingAdvisorApproval)]
    [InlineData(RegistrationStatus.Active)]
    [InlineData(RegistrationStatus.Completed)]
    public async Task D25_GonderilmisKayitVarken_AyniSubeyeYenidenGonderim_ZatenGonderildiIleReddedilir(RegistrationStatus durum)
    {
        var (veri, sube, ogrenci) = await KurAsync("DKY101");
        var kayit = await veri.KayitEkleAsync(ogrenci, sube, durum);
        await veri.MevcutVerAsync(sube, 1);

        ZatenGonderildi(await GonderAsync(veri, ogrenci, sube), sube);
        Assert.Equal(1, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (kayit, ogrenci, durum) }, await veri.KayitSatirlariAsync(sube));
    }

    // Karşı örnek: kapanmış kayıt koltuk tutmaz; yeniden gönderim kabul edilir, koltuk alınır, aynı satır onay beklemeye geçer.
    [Theory]
    [InlineData(RegistrationStatus.Rejected)]
    [InlineData(RegistrationStatus.Dropped)]
    [InlineData(RegistrationStatus.Withdrawn)]
    public async Task D25_KarsiOrnek_KapanmisKayitVarken_YenidenGonderimKabul_AyniSatirKullanilir(RegistrationStatus durum)
    {
        var (veri, sube, ogrenci) = await KurAsync("DKY101");
        var kayit = await veri.KayitEkleAsync(ogrenci, sube, durum);

        var sonuc = await GonderAsync(veri, ogrenci, sube);

        Assert.True(sonuc.Success);
        Assert.Empty(sonuc.ValidationResult!.Errors);
        Assert.Equal(1, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (kayit, ogrenci, Bekliyor) }, await veri.KayitSatirlariAsync(sube));
    }

    // Karışık istek: A şubesinde taslak, B şubesinde onay bekleyen kayıt; bir hata varsa hiçbiri gönderilmez.
    [Fact]
    public async Task D25_KarisikIstek_TaslakVeGonderilmis_HepsiReddedilir_HataYalnizGonderilmisSubede()
    {
        var (veri, subeA, ogrenci) = await KurAsync("DKY101");
        var subeB = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY102"), kapasite: 2);
        var taslak = await veri.KayitEkleAsync(ogrenci, subeA, RegistrationStatus.Draft);
        var bekleyen = await veri.KayitEkleAsync(ogrenci, subeB, Bekliyor);
        await veri.MevcutVerAsync(subeA, 1);
        await veri.MevcutVerAsync(subeB, 1);

        ZatenGonderildi(await GonderAsync(veri, ogrenci, subeA, subeB), subeB);
        Assert.Equal((1, 1), (await veri.MevcutAsync(subeA), await veri.MevcutAsync(subeB)));
        Assert.Equal(new[] { (taslak, ogrenci, RegistrationStatus.Draft) }, await veri.KayitSatirlariAsync(subeA));
        Assert.Equal(new[] { (bekleyen, ogrenci, Bekliyor) }, await veri.KayitSatirlariAsync(subeB));
    }

    // Her iki şubede de onay bekleyen kayıt varken ikisi birlikte gönderilir; her şube için ayrı hata döner, hiçbir şey değişmez.
    [Fact]
    public async Task D25_IkiSubedeDeGonderilmisKayit_IkisiDeZatenGonderildiIleReddedilir()
    {
        var (veri, subeA, ogrenci) = await KurAsync("DKY101");
        var subeB = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY102"), kapasite: 2);
        var kayitA = await veri.KayitEkleAsync(ogrenci, subeA, Bekliyor);
        var kayitB = await veri.KayitEkleAsync(ogrenci, subeB, Bekliyor);
        await veri.MevcutVerAsync(subeA, 1);
        await veri.MevcutVerAsync(subeB, 1);

        var sonuc = await GonderAsync(veri, ogrenci, subeA, subeB);

        Assert.False(sonuc.Success);
        Assert.Equal(2, sonuc.ValidationResult!.Errors.Count);
        Assert.All(sonuc.ValidationResult.Errors, h => Assert.Equal("AlreadySubmitted", h.Rule));
        Assert.Equal(new HashSet<int?> { subeA, subeB }, sonuc.ValidationResult.Errors.Select(h => (int?)h.CourseSectionId).ToHashSet());
        Assert.Equal((1, 1), (await veri.MevcutAsync(subeA), await veri.MevcutAsync(subeB)));
        Assert.Equal(new[] { (kayitA, ogrenci, Bekliyor) }, await veri.KayitSatirlariAsync(subeA));
        Assert.Equal(new[] { (kayitB, ogrenci, Bekliyor) }, await veri.KayitSatirlariAsync(subeB));
    }

    // Karşı örnek: aynı istek, B şubesindeki kayıt reddedilmiş; iki şube de gönderilir.
    [Fact]
    public async Task D25_KarsiOrnek_KarisikIstek_TaslakVeReddedilmis_IkisiDeGonderilir()
    {
        var (veri, subeA, ogrenci) = await KurAsync("DKY101");
        var subeB = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY102"), kapasite: 2);
        var taslak = await veri.KayitEkleAsync(ogrenci, subeA, RegistrationStatus.Draft);
        var reddedilen = await veri.KayitEkleAsync(ogrenci, subeB, RegistrationStatus.Rejected);
        await veri.MevcutVerAsync(subeA, 1);

        var sonuc = await GonderAsync(veri, ogrenci, subeA, subeB);

        Assert.True(sonuc.Success);
        Assert.Empty(sonuc.ValidationResult!.Errors);
        Assert.Equal((1, 1), (await veri.MevcutAsync(subeA), await veri.MevcutAsync(subeB)));
        Assert.Equal(new[] { (taslak, ogrenci, Bekliyor) }, await veri.KayitSatirlariAsync(subeA));
        Assert.Equal(new[] { (reddedilen, ogrenci, Bekliyor) }, await veri.KayitSatirlariAsync(subeB));
    }

    // Yarış: ön denetimden sonra, işlem içindeki ilk kaydetmeden hemen önce aynı öğrenci-şube için onay bekleyen satır
    // ayrı bağlantıyla eklenir ve kesinleşir; şube satırına dokunulmaz. Gönderim geri alınır, araya giren satır kalır.
    [Fact]
    public async Task D25_Yaris_OnDenetimdenSonraAyniSubeyeBekleyenKayitEklenir_GonderimGeriAlinir()
    {
        var (veri, sube, ogrenci) = await KurAsync("DKY101");
        var arayaGiren = 0;
        var kesici = new ArayaGirenKesici(async () => arayaGiren = await veri.KayitEkleAsync(ogrenci, sube, Bekliyor));

        RegistrationResponseDto sonuc;
        await using (var kesicili = veri.KesiciliContext(kesici))
            sonuc = await veri.GonderAsync(kesicili, ogrenci, sube);

        Assert.Equal(1, kesici.Calisma);
        ZatenGonderildi(sonuc, sube);
        Assert.Equal(0, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (arayaGiren, ogrenci, Bekliyor) }, await veri.KayitSatirlariAsync(sube));
    }

    // Karşı örnek: araya giren satır başka bir öğrencinin; iki kayıt da kalır, gönderim kabul edilir.
    [Fact]
    public async Task D25_KarsiOrnek_Yaris_ArayaGirenBaskaOgrenci_GonderimKabul()
    {
        var (veri, sube, ogrenci) = await KurAsync("DKY101");
        var baska = await veri.OgrenciEkleAsync("02");
        var kesici = new ArayaGirenKesici(async () => await veri.KayitEkleAsync(baska, sube, Bekliyor));

        RegistrationResponseDto sonuc;
        await using (var kesicili = veri.KesiciliContext(kesici))
            sonuc = await veri.GonderAsync(kesicili, ogrenci, sube);

        Assert.Equal(1, kesici.Calisma);
        Assert.True(sonuc.Success);
        Assert.Equal(1, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (ogrenci, Bekliyor), (baska, Bekliyor) }.OrderBy(k => k.Item1), await veri.KayitlarAsync(sube));
    }

    private async Task<(DersKaydiVeriKurucu, int, int)> KurAsync(string dersKodu)
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync(dersKodu), kapasite: 2);
        return (veri, sube, await veri.OgrenciEkleAsync("01"));
    }

    private async Task<RegistrationResponseDto> GonderAsync(DersKaydiVeriKurucu veri, int ogrenci, params int[] subeler)
    {
        await using var db = fixture.YeniContext();
        return await veri.GonderAsync(db, ogrenci, subeler);
    }

    private static void ZatenGonderildi(RegistrationResponseDto sonuc, int sube)
    {
        Assert.False(sonuc.Success);
        var hata = Assert.Single(sonuc.ValidationResult!.Errors);
        Assert.Equal("AlreadySubmitted", hata.Rule);
        Assert.Equal(sube, hata.CourseSectionId);
        Assert.Contains("dersine kaydınız zaten gönderildi", hata.Message);
    }
}
