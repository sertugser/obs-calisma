using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Postgres.DersKaydi;

// D23: araya giren eşzamanlı gönderim, koltuk boşken gönderimi reddetmez; çakışan işlem taze okumayla yeniden denenir.
// Araya girme kesiciyle, işlem içindeki kaydetmeden hemen önce ve belirlenimli kurulur; her araya giren gönderim ayrı context'le.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class D23_EszamanliGonderimTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const RegistrationStatus Bekliyor = RegistrationStatus.PendingAdvisorApproval;

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task D23_BosKoltukVarken_ArayaGirenGonderim_AyniSube_IkisiDeKabul_TekBildirim(int kapasite)
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");
        var danisman = await veri.DanismanAtaAsync(bir);

        RegistrationResponseDto? araSonuc = null;
        var kesici = new ArayaGirenKesici(async () =>
        {
            await using var db = fixture.YeniContext();
            araSonuc = await veri.GonderAsync(db, iki, sube);
        });
        await using var kesicili = veri.KesiciliContext(kesici);
        var sonuc = await veri.BildirimliGonderAsync(kesicili, bir, sube);

        Assert.Equal(1, kesici.Calisma);
        Assert.True(araSonuc!.Success);
        Kabul(sonuc, sube);
        Assert.Equal(2, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (bir, Bekliyor), (iki, Bekliyor) }.OrderBy(k => k.Item1), await veri.KayitlarAsync(sube));
        Assert.Equal(1, await veri.OnayBildirimiSayisiAsync(danisman));
    }

    // Çakışma commit anında: araya giren gönderim, kaydetmeden sonra ve onaydan önce diğer şubeye yazar. Kaydetme kabul
    // edildiği için geri sarma yeniden etkinleştirilen kaydı veritabanındaki hâline döndürmelidir; aksi hâlde durum yazılmaz.
    [Fact]
    public async Task D23_CaprazListe_BirakilmisKayit_OnayAnindaCakisma_YenidenDenemedeAyniSatirOnayBeklemeye()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var subeA = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 3, grup: 1);
        var subeB = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY102"), kapasite: 3, grup: 1);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");
        var kayit = await veri.KayitEkleAsync(bir, subeA, RegistrationStatus.Dropped);
        var kesici = new OnayOncesiKesici(async () =>
        {
            await using var db = fixture.YeniContext();
            Assert.True((await veri.GonderAsync(db, iki, subeB)).Success);
        });

        await using var kesicili = veri.KesiciliContext(kesici);
        var sonuc = await veri.GonderAsync(kesicili, bir, subeA);

        Assert.Equal((1, 2), (kesici.Calisma, kesici.Onay));
        Kabul(sonuc, subeA);
        Assert.Equal((1, 1), (await veri.MevcutAsync(subeA), await veri.MevcutAsync(subeB)));
        Assert.Equal(new[] { (kayit, bir, Bekliyor) }, await veri.KayitSatirlariAsync(subeA));
        Assert.Equal(new[] { (iki, Bekliyor) }, await veri.KayitlarAsync(subeB));
    }

    // Gerçek denetim kesicisiyle; çakışmada araya giren gönderim "AuditLogs" ekleme komutundan (iç kaydetme) hemen önce diğer şubeye
    // yazar. Çakışmasız gönderimdeki denetim satırları değişmez; çakışmada yalnız son denemenin satırları, kendi zamanıyla yazılır.
    // Oluşturma satırının kimliği bugün kaydetme öncesindeki geçici değerdir; karşılaştırılmaz (mevcut davranış).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task D23_CaprazListe_DenetimSatirlari_YalnizSonDenemedenYazilir(bool cakisma)
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var subeA = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2, grup: 1);
        var subeB = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY102"), kapasite: 2, grup: 1);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");
        var arayaGirmeSonu = DateTime.MinValue;
        var kesici = new KomutOncesiKesici(k => cakisma && k.CommandText.Contains("INSERT INTO \"AuditLogs\""), async () =>
        {
            await using var db = fixture.YeniContext();
            Assert.True((await veri.GonderAsync(db, iki, subeB)).Success);
            arayaGirmeSonu = DateTime.UtcNow;
        });

        await using var denetimli = veri.KesiciliContext(new AuditInterceptor(new HttpContextAccessor()), kesici);
        var sonuc = await veri.GonderAsync(denetimli, bir, subeA);

        Assert.Equal(cakisma ? 1 : 0, kesici.Calisma);
        Kabul(sonuc, subeA);
        Assert.Equal((1, cakisma ? 1 : 0), (await veri.MevcutAsync(subeA), await veri.MevcutAsync(subeB)));
        await using var oku = fixture.YeniContext();
        var satirlar = await oku.AuditLogs.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(
            [("CourseRegistration", "Created", null), ("CourseSection", "Modified", subeA.ToString()), ("CourseRegistration", "Modified", sonuc.Registrations[0].Id.ToString())],
            satirlar.Select(a => (a.EntityType, a.Action, a.Action == "Created" ? null : a.EntityId)));
        Assert.All(satirlar, a => Assert.True(a.Timestamp > arayaGirmeSonu, $"{a.EntityType}/{a.Action} ilk denemeden"));
        Assert.Contains("\"enrolledCount\":1", satirlar[1].NewValue);
    }

    private static void Kabul(RegistrationResponseDto sonuc, int sube)
    {
        Assert.True(sonuc.Success, string.Join(" | ", sonuc.ValidationResult?.Errors.Select(h => h.Message) ?? []));
        Assert.Empty(sonuc.ValidationResult!.Errors);
        Assert.Equal(sube, Assert.Single(sonuc.Registrations).CourseSectionId);
    }
}
