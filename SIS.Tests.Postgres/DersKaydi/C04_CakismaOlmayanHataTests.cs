using SIS.Domain.Enums;

namespace SIS.Tests.Postgres.DersKaydi;

// C-04 / D25: aynı öğrencinin aynı şubeye ikinci gönderimi tekil dizine kadar gitmez; ön denetim anlamlı bir retle durdurur.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class C04_CakismaOlmayanHataTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const RegistrationStatus Bekliyor = RegistrationStatus.PendingAdvisorApproval;

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // İkinci gönderim istisna atmaz: tek hata AlreadySubmitted, mevcut ve kayıt değişmez.
    [Fact]
    public async Task C04_AyniOgrenciAyniSubeyeIkinciGonderim_ZatenGonderildiIleReddedilir_MevcutDegismez()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");
        await using (var db = fixture.YeniContext())
            Assert.True((await veri.GonderAsync(db, bir, sube)).Success);

        await using (var db = fixture.YeniContext())
        {
            var sonuc = await veri.GonderAsync(db, bir, sube);
            Assert.False(sonuc.Success);
            var hata = Assert.Single(sonuc.ValidationResult!.Errors);
            Assert.Equal("AlreadySubmitted", hata.Rule);
            Assert.Equal(sube, hata.CourseSectionId);
        }
        Assert.Equal(1, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (bir, Bekliyor) }, await veri.KayitlarAsync(sube));

        // Karşı örnek: başka bir öğrencinin aynı şubeye gönderimi kabul edilir.
        await using (var db = fixture.YeniContext())
            Assert.True((await veri.GonderAsync(db, iki, sube)).Success);
        Assert.Equal(2, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (bir, Bekliyor), (iki, Bekliyor) }.OrderBy(k => k.Item1), await veri.KayitlarAsync(sube));
    }
}
