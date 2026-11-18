using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Enums;

namespace SIS.Tests.Postgres.DersKaydi;

// C-05: çapraz liste grubundaki şubeler kapasiteyi paylaşır; doğrulama ve gönderimler sıralı, her biri ayrı context'le.
// Doğrulama yalnız şubenin kendi mevcuduna bakar; gönderim grup toplamını şubenin kendi kapasitesiyle karşılaştırır.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class C05_CaprazListeKontenjanTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const RegistrationStatus Bekliyor = RegistrationStatus.PendingAdvisorApproval;

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Bilinen hata H11: doğrulama grup toplamını saymıyor; grup doluyken "uygun" diyor.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task C05_GrupDolu_DogrulamaUygunDiyor()
    {
        var (veri, a, b, o) = await GrupKurAsync();
        await KabulAsync(veri, o[1], b);
        // Kurulumun canlılığı: grup gerçekten dolu (A'da 1, B'de 1; kapasite 2).
        Assert.Equal((1, 1), (await veri.MevcutAsync(a), await veri.MevcutAsync(b)));

        var dogrulama = await veri.DogrulaAsync(o[2], a);
        Assert.True(dogrulama.IsValid);
        Assert.Empty(dogrulama.Errors);

        // Kontrol: şubenin kendisi dolu (mevcut 3, kapasite 2) ise doğrulama reddeder; mesajda mevcut ve kapasite ayrı sayılardır.
        var c = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY106"), kapasite: 2, grup: 4);
        await veri.MevcutVerAsync(c, 3);
        TekKontenjanHatasi(await veri.DogrulaAsync(o[2], c), "dersinin kontenjani dolu (3/2).");
    }

    [Fact]
    public async Task C05_GrupDolu_GonderimBuEsnadaDolduIleReddedilir_MesajdaGrupToplami()
    {
        // Grup toplamı (1 + 2 = 3) ölçülen şubenin kapasitesinden (2) farklıdır: mesajdaki iki sayı ayrı sayılardır.
        var (veri, a, b, o) = await GrupKurAsync();
        await KabulAsync(veri, o[1], b);
        await KabulAsync(veri, o[3], b);

        RegistrationResponseDto sonuc;
        await using (var db = fixture.YeniContext())
            sonuc = await veri.GonderAsync(db, o[2], a);

        Assert.False(sonuc.Success);
        TekKontenjanHatasi(sonuc.ValidationResult!, "dersinin kontenjanı bu esnada doldu (3/2).");
        Assert.Equal((1, 2), (await veri.MevcutAsync(a), await veri.MevcutAsync(b)));
        Assert.Equal(new[] { (o[0], Bekliyor) }, await veri.KayitlarAsync(a));
        Assert.Equal(new[] { (o[1], Bekliyor), (o[3], Bekliyor) }.OrderBy(k => k.Item1), await veri.KayitlarAsync(b));
    }

    // Bugünkü davranış: iptal edilmiş bir grup üyesinin mevcudu bugün grup toplamına girer. Dönem iptali şubeleri iptal eder ama
    // mevcudu sıfırlamaz; iptal geri alınınca şubeler kendiliğinden açılmaz, biri yeniden açılınca iptal kalan üye toplamda sayılır.
    [Fact]
    public async Task C05_GrupUyesiIptalEdilmisVeMevcutluOlunca_ToplamaGirer_GonderimReddedilir()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var a = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2, grup: 1);
        var b = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY102"), kapasite: 2, grup: 1);
        await veri.MevcutVerAsync(b, 2);
        await veri.SubeIptalEtAsync(b);
        var ogrenci = await veri.OgrenciEkleAsync("01");

        RegistrationResponseDto sonuc;
        await using (var db = fixture.YeniContext())
            sonuc = await veri.GonderAsync(db, ogrenci, a);

        Assert.False(sonuc.Success);
        TekKontenjanHatasi(sonuc.ValidationResult!, "dersinin kontenjanı bu esnada doldu (2/2).");
        Assert.Equal((0, 2), (await veri.MevcutAsync(a), await veri.MevcutAsync(b)));
        Assert.Empty(await veri.KayitlarAsync(a));
    }

    // Karşı örnek: aynı kurulum, yalnız grup arkadaşı B boş; gruba ait olmayan dolu şubeler sayılmaz.
    [Fact]
    public async Task C05_KarsiOrnek_GrupArkadasiBos_GrupDisiDoluSubelerSayilmaz_Kabul()
    {
        var (veri, a, b, o) = await GrupKurAsync();

        RegistrationResponseDto sonuc;
        await using (var db = fixture.YeniContext())
            sonuc = await veri.GonderAsync(db, o[2], a);

        Assert.True(sonuc.Success);
        Assert.Empty(sonuc.ValidationResult!.Errors);
        Assert.Equal((2, 0), (await veri.MevcutAsync(a), await veri.MevcutAsync(b)));
        Assert.Equal(new[] { (o[0], Bekliyor), (o[2], Bekliyor) }.OrderBy(k => k.Item1), await veri.KayitlarAsync(a));
    }

    // A ve B aynı grupta (kimlik 2); A'nın kapasitesi 2, B'nin 4; ö1 A'ya kayıtlı. Gruba ait olmayan dolu şubeler: daha küçük kimlikli
    // grup (1), grupsuz, başka dönemde daha büyük kimlikli grup (3).
    private async Task<(DersKaydiVeriKurucu Veri, int A, int B, int[] Ogrenciler)> GrupKurAsync()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var a = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2, grup: 2);
        var b = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY102"), kapasite: 4, grup: 2);
        await veri.MevcutVerAsync(await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY103"), kapasite: 5, grup: 1), 5);
        await veri.MevcutVerAsync(await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY104"), kapasite: 5), 5);
        await veri.BaskaDonemDoluSubeEkleAsync(await veri.DersEkleAsync("DKY105"), kapasite: 5, grup: 3);
        int[] o = [await veri.OgrenciEkleAsync("01"), await veri.OgrenciEkleAsync("02"), await veri.OgrenciEkleAsync("03"), await veri.OgrenciEkleAsync("04")];
        await KabulAsync(veri, o[0], a);
        return (veri, a, b, o);
    }

    private async Task KabulAsync(DersKaydiVeriKurucu veri, int ogrenci, int sube)
    {
        await using var db = fixture.YeniContext();
        Assert.True((await veri.GonderAsync(db, ogrenci, sube)).Success);
    }

    private static void TekKontenjanHatasi(ValidationResultDto sonuc, string mesajParcasi)
    {
        Assert.False(sonuc.IsValid);
        var hata = Assert.Single(sonuc.Errors);
        Assert.Equal("Capacity", hata.Rule);
        Assert.Contains(mesajParcasi, hata.Message);
    }
}
