using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Tests.Postgres.DersProgrami;
using SIS.Tests.Postgres.Yetki;

namespace SIS.Tests.Postgres.NotHesabi;

// B-02 (HTTP): sınav yönergesi oluşturma ve güncelleme isteğinde geçme notu alanının eşlemesi. Denetleyici düzeyi
// SIS.Tests'teki B02_GecmeNotuTests ile sabit. İstekler arasındaki tek fark passingGrade alanı (güncellemede bir de ad); 37 gövdedeki
// başka hiçbir sayıyla aynı değil. D37: alan gönderilmezse 0 yazılmıyor, istek 400 ile reddediliyor.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class B02_GecmeNotuHttpTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Uc = "/api/admin/exam-regulations";
    private const string Email = "b02h-personel@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task B02_OlusturmaIstegindeGecmeNotuYokken400_KayitYok()
    {
        var (istemci, programId) = await KurAsync();

        var yanit = await istemci.PostAsJsonAsync(Uc, Govde(programId, null));

        await HataAsync(yanit);
        await using var db = fixture.YeniContext();
        Assert.False(await db.ExamRegulations.AnyAsync());
    }

    // Karşı örnek: aynı istek, alan açık değerle.
    [Fact]
    public async Task B02_OlusturmaIstegindeGecmeNotu37ile200_KaliciDeger37()
    {
        var (istemci, programId) = await KurAsync();

        var yanit = await istemci.PostAsJsonAsync(Uc, Govde(programId, 37m));

        Assert.Equal((37m, 37m), await SonucAsync(yanit));
    }

    // Önceki değer 37 (karşı örnekteki istekle yazılır); güncelleme gövdesinde alan yok ve ad farklı: hiçbir alan yazılmamalı.
    [Fact]
    public async Task B02_GuncellemeIstegindeGecmeNotuYokken400_KaliciDeger37Kalir()
    {
        var (istemci, programId) = await KurAsync();
        var olusan = await istemci.PostAsJsonAsync(Uc, Govde(programId, 37m));
        Assert.Equal((37m, 37m), await SonucAsync(olusan));
        int id;
        await using (var db = fixture.YeniContext())
            id = await db.ExamRegulations.Select(r => r.Id).SingleAsync();

        var yanit = await istemci.PutAsJsonAsync($"{Uc}/{id}", Govde(programId, null, "Yeni Ad"));

        await HataAsync(yanit);
        await using var yeni = fixture.YeniContext();
        var kalici = await yeni.ExamRegulations.AsNoTracking().Where(r => r.Id == id).Select(r => new { r.PassingGrade, r.Name }).SingleAsync();
        Assert.Equal((37m, "Deneme Yönergesi"), (kalici.PassingGrade, kalici.Name));
    }

    /// <summary>Yönerge oluşturma ve düzenleme izinli rol (sistem yöneticisi değil); KapsamVeriKurucu'nun programı.</summary>
    private async Task<(HttpClient, int ProgramId)> KurAsync()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("AdministrativeStaff", "menu.sinav-mufredati.create", "menu.sinav-mufredati.edit");
        var kullanici = await kurucu.KullaniciAsync(Email, "AdministrativeStaff");
        var veri = await KapsamVeriKurucu.KurAsync(fixture, kullanici);
        return (await kurucu.IstemciAsync(Email), veri.ProgramId);
    }

    private static Dictionary<string, object> Govde(int programId, decimal? gecmeNotu, string ad = "Deneme Yönergesi")
    {
        var govde = new Dictionary<string, object>
        {
            ["programId"] = programId, ["degreeLevel"] = "Bachelor", ["termType"] = "Fall", ["year"] = 2045, ["isActive"] = true,
            ["name"] = ad, ["midtermCount"] = 1, ["finalCount"] = 1,
            ["minMidtermWeightPercent"] = 40, ["maxMidtermWeightPercent"] = 60, ["minFinalWeightPercent"] = 40, ["maxFinalWeightPercent"] = 60,
            ["rawScoreFloor1"] = 0, ["rawScoreFloor2"] = 0, ["finalExamFloor"] = 0,
        };
        if (gecmeNotu is { } deger)
            govde["passingGrade"] = deger;
        return govde;
    }

    // 400 iddiası; gövdede tek hata iletisi.
    private static async Task HataAsync(HttpResponseMessage yanit)
    {
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal("Geçme notu gönderilmelidir.", govde.RootElement.GetProperty("message").GetString());
    }

    // 200 iddiası; dönen geçme notu ve tek yönergenin kalıcı geçme notu (yeni bir context'le).
    private async Task<(decimal Donen, decimal Kalici)> SonucAsync(HttpResponseMessage yanit)
    {
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var id = govde.RootElement.GetProperty("id").GetInt32();
        await using var db = fixture.YeniContext();
        var kalici = await db.ExamRegulations.AsNoTracking().Where(r => r.Id == id).Select(r => r.PassingGrade).SingleAsync();
        return (govde.RootElement.GetProperty("passingGrade").GetDecimal(), kalici);
    }
}
