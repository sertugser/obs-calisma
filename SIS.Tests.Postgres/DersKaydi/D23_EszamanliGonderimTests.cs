using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
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

    // Çapraz listedeki iki şube iki koltuğu paylaşır; araya giren gönderim diğer şubeye yazar.
    [Fact]
    public async Task D23_CaprazListe_BosKoltukVarken_ArayaGirenGonderim_DigerSube_IkisiDeKabul()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var subeA = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2, grup: 1);
        var subeB = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY102"), kapasite: 2, grup: 1);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");

        var (sonuc, kesici) = await ArayaGirerekGonderAsync(veri, bir, subeA, [iki], subeB);

        Assert.Equal(1, kesici.Calisma);
        Kabul(sonuc, subeA);
        Assert.Equal((1, 1), (await veri.MevcutAsync(subeA), await veri.MevcutAsync(subeB)));
        Assert.Equal(new[] { (bir, Bekliyor) }, await veri.KayitlarAsync(subeA));
        Assert.Equal(new[] { (iki, Bekliyor) }, await veri.KayitlarAsync(subeB));
    }

    // Her denemeye başka bir öğrencinin gönderimi girer: tam üç deneme yapılır, sonra bugünkü "tekrar deneyin" reti döner.
    [Fact]
    public async Task D23_HerDenemedeArayaGirenGonderim_UcDenemedenSonraTekrarDeneyinIleReddedilir()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 10);
        var bir = await veri.OgrenciEkleAsync("01");
        var arayaGirenler = new List<int>();
        for (var no = 2; no <= 6; no++)
            arayaGirenler.Add(await veri.OgrenciEkleAsync("0" + no));

        var (sonuc, kesici) = await ArayaGirerekGonderAsync(veri, bir, sube, arayaGirenler, sube);

        Assert.Equal(3, kesici.Calisma);
        Assert.False(sonuc.Success);
        var hata = Assert.Single(sonuc.ValidationResult!.Errors);
        Assert.Equal("Capacity", hata.Rule);
        Assert.Contains("kontenjanı bu esnada değişti. Lütfen tekrar deneyin.", hata.Message);
        Assert.Equal(3, await veri.MevcutAsync(sube));
        Assert.Equal(arayaGirenler.Take(3).Select(o => (o, Bekliyor)), await veri.KayitlarAsync(sube));
    }

    // Öğrencinin bırakılmış kaydı yeniden etkinleşir; çakışan denemenin izleri geri sarılır: aynı satır, tek koltuk.
    [Fact]
    public async Task D23_BirakilmisKayit_ArayaGirenGonderim_AyniSatirYenidenKullanilir_KoltukBirKezSayilir()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");
        var kayit = await veri.KayitEkleAsync(bir, sube, RegistrationStatus.Dropped);

        var (sonuc, kesici) = await ArayaGirerekGonderAsync(veri, bir, sube, [iki], sube);

        Assert.Equal(1, kesici.Calisma);
        Kabul(sonuc, sube);
        Assert.Equal(2, await veri.MevcutAsync(sube));
        var satirlar = await veri.KayitSatirlariAsync(sube);
        Assert.Equal(new[] { (kayit, bir, Bekliyor), (satirlar[^1].Item1, iki, Bekliyor) }, satirlar);
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

    // Araya giren, aynı öğrencinin aynı bırakılmış kaydı yeniden gönderen ikinci isteği: öğrencinin kendi kaydı değiştiği için
    // yeniden denenmez (yeniden deneme koltuğu ikinci kez sayardı); bugünkü "tekrar deneyin" reti döner.
    [Fact]
    public async Task D23_AyniOgrencininArayaGirenGonderimi_BirakilmisKayit_YenidenDenenmez_KoltukBirKezSayilir()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2);
        var bir = await veri.OgrenciEkleAsync("01");
        var kayit = await veri.KayitEkleAsync(bir, sube, RegistrationStatus.Dropped);

        var (sonuc, kesici) = await ArayaGirerekGonderAsync(veri, bir, sube, [bir, bir, bir], sube);

        Assert.Equal(1, kesici.Calisma);
        Assert.False(sonuc.Success);
        var hata = Assert.Single(sonuc.ValidationResult!.Errors);
        Assert.Equal("Capacity", hata.Rule);
        Assert.Contains("kontenjanı bu esnada değişti. Lütfen tekrar deneyin.", hata.Message);
        Assert.Equal(1, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (kayit, bir, Bekliyor) }, await veri.KayitSatirlariAsync(sube));
    }

    // D25 yarışı: araya aynı öğrenci-şube için onay bekleyen satır girer; tekil dizin ihlali yeniden denenmez.
    [Fact]
    public async Task D23_D25Yarisi_AyniOgrenciSubeKaydi_ZatenGonderildi_YenidenDenenmez()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2);
        var bir = await veri.OgrenciEkleAsync("01");
        var arayaGiren = 0;
        var kesici = new ArayaGirenKesici(async () => arayaGiren = await veri.KayitEkleAsync(bir, sube, Bekliyor), enCok: 3);

        await using var kesicili = veri.KesiciliContext(kesici);
        var sonuc = await veri.GonderAsync(kesicili, bir, sube);

        Assert.Equal(1, kesici.Calisma);
        Assert.False(sonuc.Success);
        var hata = Assert.Single(sonuc.ValidationResult!.Errors);
        Assert.Equal("AlreadySubmitted", hata.Rule);
        Assert.Equal(sube, hata.CourseSectionId);
        Assert.Equal(0, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (arayaGiren, bir, Bekliyor) }, await veri.KayitSatirlariAsync(sube));
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

    // Geri sarmada yeniden yüklenen bırakılmış kayıt araya giren bir silmeyle yok olmuşsa yeniden denenmez: "tekrar deneyin".
    [Fact]
    public async Task D23_GeriSarmadaKayitSilinmis_YenidenDenenmez_TekrarDeneyinIleReddedilir()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2);
        var bir = await veri.OgrenciEkleAsync("01");
        var iki = await veri.OgrenciEkleAsync("02");
        var kayit = await veri.KayitEkleAsync(bir, sube, RegistrationStatus.Dropped);
        var arayaGiren = new ArayaGirenKesici(async () =>
        {
            await using var db = fixture.YeniContext();
            Assert.True((await veri.GonderAsync(db, iki, sube)).Success);
        });
        var silici = new KomutOncesiKesici(k => arayaGiren.Calisma == 1 && k.CommandText.Contains("FROM \"CourseRegistrations\"")
            && k.CommandText.Contains("\"Id\" = @"), async () =>
        {
            await using var db = fixture.YeniContext();
            await db.CourseRegistrations.Where(r => r.Id == kayit).ExecuteDeleteAsync();
        });

        await using var kesicili = veri.KesiciliContext(arayaGiren, silici);
        var sonuc = await veri.GonderAsync(kesicili, bir, sube);

        Assert.Equal((1, 1), (arayaGiren.Calisma, silici.Calisma));
        Assert.False(sonuc.Success);
        Assert.Contains("kontenjanı bu esnada değişti. Lütfen tekrar deneyin.", Assert.Single(sonuc.ValidationResult!.Errors).Message);
        Assert.Equal(1, await veri.MevcutAsync(sube));
        Assert.Equal(new[] { (iki, Bekliyor) }, await veri.KayitlarAsync(sube));
    }

    // Çakışma dışı veritabanı hatası (yabancı anahtar ihlali) yeniden denenmez: tek deneme, istisna yayılır, hiçbir şey yazılmaz.
    [Fact]
    public async Task D23_CakismaDisiVeritabaniHatasi_YenidenDenenmez_IstisnaYayilir()
    {
        var veri = await DersKaydiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(await veri.DersEkleAsync("DKY101"), kapasite: 2);
        var bir = await veri.OgrenciEkleAsync("01");
        AppDbContext? kesicili = null;
        var kesici = new ArayaGirenKesici(() =>
        {
            kesicili!.ChangeTracker.Entries<CourseRegistration>().Single(e => e.State == EntityState.Added).Entity.StudentId = int.MaxValue;
            return Task.CompletedTask;
        }, enCok: 3);

        await using var db = kesicili = veri.KesiciliContext(kesici);
        var hata = await Assert.ThrowsAsync<DbUpdateException>(() => veri.GonderAsync(db, bir, sube));

        Assert.Equal("23503", Assert.IsType<PostgresException>(hata.InnerException).SqlState);
        Assert.Equal(1, kesici.Calisma);
        Assert.Equal(0, await veri.MevcutAsync(sube));
        Assert.Empty(await veri.KayitlarAsync(sube));
    }

    // Öğrenci kesicili context'le gönderir; kesici her denemenin kaydetmesinden önce sıradaki araya girenin gönderimini
    // ayrı context'le tamamlar (en çok araya giren sayısı kadar). Her araya giren gönderim kabul edilmelidir.
    private async Task<(RegistrationResponseDto Sonuc, ArayaGirenKesici Kesici)> ArayaGirerekGonderAsync(
        DersKaydiVeriKurucu veri, int ogrenci, int sube, IReadOnlyList<int> arayaGirenler, int arayaGirenSube)
    {
        var sira = new Queue<int>(arayaGirenler);
        var kesici = new ArayaGirenKesici(async () =>
        {
            await using var db = fixture.YeniContext();
            Assert.True((await veri.GonderAsync(db, sira.Dequeue(), arayaGirenSube)).Success);
        }, enCok: arayaGirenler.Count);
        await using var kesicili = veri.KesiciliContext(kesici);
        return (await veri.GonderAsync(kesicili, ogrenci, sube), kesici);
    }

    private static void Kabul(RegistrationResponseDto sonuc, int sube)
    {
        Assert.True(sonuc.Success, string.Join(" | ", sonuc.ValidationResult?.Errors.Select(h => h.Message) ?? []));
        Assert.Empty(sonuc.ValidationResult!.Errors);
        Assert.Equal(sube, Assert.Single(sonuc.Registrations).CourseSectionId);
    }
}
