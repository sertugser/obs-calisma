using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Enums;

namespace SIS.Tests.Postgres.DersKaydi;

// C-04: son koltuğa iki öğrencinin gönderimi; eşzamanlılık ayrı context'lerle, sıralı ve belirlenimli kurulur.
// Ret mesajları yolu ayırır: doğrulama "kontenjani dolu", rezervasyon "bu esnada doldu", çakışma "bu esnada değişti".
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class C04_KontenjanEszamanlilikTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const RegistrationStatus Bekliyor = RegistrationStatus.PendingAdvisorApproval;

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Öğrenci 1'in context'i şubeyi öğrenci 2'nin kaydından önce okumuş: doğrulama eski değerle geçer, rezervasyon taze okuyup reddeder.
    [Fact]
    public async Task C04_SonKoltuk_OnceOkunmusSube_RezervasyondaBuEsnadaDolduIleReddedilir()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 1);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");

        await using var eski = fixture.YeniContext();
        Assert.Equal(0, (await eski.CourseSections.SingleAsync(s => s.Id == sube)).EnrolledCount);
        await using (var db = fixture.YeniContext())
            Assert.True((await veri.GonderAsync(db, iki, sube)).Success);

        TekKontenjanHatasi(await veri.GonderAsync(eski, bir, sube), "dersinin kontenjanı bu esnada doldu (1/1).");

        // Kontrol: şubeyi önceden okumamış context'te ret doğrulamadan gelir.
        await using (var taze = fixture.YeniContext())
            TekKontenjanHatasi(await veri.GonderAsync(taze, bir, sube), "dersinin kontenjani dolu (1/1).");

        Assert.Equal(1, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (iki, Bekliyor) }, await veri.KayitlarAsync(sube));
    }

    // Karşı örnek: aynı sıra, iki koltuk; iki gönderim de kabul edilir.
    [Fact]
    public async Task C04_KarsiOrnek_IkiKoltuk_OnceOkunmusSube_IkiGonderimdeKabul()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");

        await using var eski = fixture.YeniContext();
        Assert.Equal(0, (await eski.CourseSections.SingleAsync(s => s.Id == sube)).EnrolledCount);
        await using (var db = fixture.YeniContext())
            Assert.True((await veri.GonderAsync(db, iki, sube)).Success);

        var sonuc = await veri.GonderAsync(eski, bir, sube);
        Assert.True(sonuc.Success);
        Assert.Empty(sonuc.ValidationResult!.Errors);
        Assert.Equal(2, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (bir, Bekliyor), (iki, Bekliyor) }.OrderBy(k => k.Item1), await veri.KayitlarAsync(sube));
    }

    // Öğrenci 2'nin gönderimi, öğrenci 1'in işlemi koltuğu okuduktan sonra ve yazmadan önce tamamlanır.
    [Fact]
    public async Task C04_SonKoltuk_ArayaGirenGonderim_AyniSube_CakismaIleReddedilir()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 1);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");

        var (sonuc, araSonuc) = await ArayaGirerekGonderAsync(veri, bir, sube, iki, sube);

        Assert.True(araSonuc.Success);
        TekKontenjanHatasi(sonuc, "kontenjanı bu esnada değişti. Lütfen tekrar deneyin.");
        Assert.Equal(1, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (iki, Bekliyor) }, await veri.KayitlarAsync(sube));
    }

    // Çapraz listedeki iki şube tek koltuğu paylaşır; araya giren gönderim diğer şubeye yazar (farklı satır).
    [Fact]
    public async Task C04_CaprazListe_SonKoltuk_ArayaGirenGonderim_DigerSube_CakismaIleReddedilir()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var subeA = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 1, grup: 1);
        var subeB = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY102"), kapasite: 1, grup: 1);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");

        var (sonuc, araSonuc) = await ArayaGirerekGonderAsync(veri, bir, subeA, iki, subeB);

        Assert.True(araSonuc.Success);
        TekKontenjanHatasi(sonuc, "kontenjanı bu esnada değişti. Lütfen tekrar deneyin.");
        Assert.Equal((0, 1), (await veri.MevcutAsync(subeA), await veri.MevcutAsync(subeB)));
        Assert.Empty(await veri.KayitlarAsync(subeA));
        Assert.Equal(new[] { (iki, Bekliyor) }, await veri.KayitlarAsync(subeB));
    }

    // Karşı örnek: araya giren gönderim kontenjanı paylaşmayan başka bir derse; iki gönderim de kabul edilir.
    [Fact]
    public async Task C04_KarsiOrnek_ArayaGirenGonderim_IlgisizDers_IkisiDeKabul()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var subeA = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 1);
        var subeB = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY102"), kapasite: 1);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");

        var (sonuc, araSonuc) = await ArayaGirerekGonderAsync(veri, bir, subeA, iki, subeB);

        Assert.True(araSonuc.Success);
        Assert.True(sonuc.Success);
        Assert.Empty(sonuc.ValidationResult!.Errors);
        Assert.Equal((1, 1), (await veri.MevcutAsync(subeA), await veri.MevcutAsync(subeB)));
        Assert.Equal(new[] { (bir, Bekliyor) }, await veri.KayitlarAsync(subeA));
        Assert.Equal(new[] { (iki, Bekliyor) }, await veri.KayitlarAsync(subeB));
    }

    // Öğrenci 1 kesicili context'le gönderir; kesici, işlem içindeki ilk kaydetmeden önce öğrenci 2'nin gönderimini ayrı context'le tamamlar.
    private async Task<(RegistrationResponseDto Sonuc, RegistrationResponseDto AraSonuc)> ArayaGirerekGonderAsync(
        DersKaydiVeriKurucu veri, int ogrenci, int sube, int arayaGiren, int arayaGirenSube)
    {
        RegistrationResponseDto? araSonuc = null;
        var kesici = new ArayaGirenKesici(async () =>
        {
            await using var db = fixture.YeniContext();
            araSonuc = await veri.GonderAsync(db, arayaGiren, arayaGirenSube);
        });
        await using var kesicili = veri.KesiciliContext(kesici);
        var sonuc = await veri.GonderAsync(kesicili, ogrenci, sube);
        Assert.Equal(1, kesici.Calisma);
        return (sonuc, araSonuc!);
    }

    private static void TekKontenjanHatasi(RegistrationResponseDto sonuc, string mesajParcasi)
    {
        Assert.False(sonuc.Success);
        var hata = Assert.Single(sonuc.ValidationResult!.Errors);
        Assert.Equal("Capacity", hata.Rule);
        Assert.Contains(mesajParcasi, hata.Message);
    }
}
