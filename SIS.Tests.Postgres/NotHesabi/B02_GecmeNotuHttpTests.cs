using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Tests.Postgres.DersProgrami;
using SIS.Tests.Postgres.Yetki;

namespace SIS.Tests.Postgres.NotHesabi;

// B-02 (HTTP): sınav yönergesi oluşturma ve güncelleme isteğinde geçme notu alanının eşlemesi. Denetleyici düzeyi
// SIS.Tests'teki B02_GecmeNotuTests ile sabit. İstekler arasındaki tek fark passingGrade alanı; 37 gövdedeki başka hiçbir sayıyla aynı değil.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class B02_GecmeNotuHttpTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Uc = "/api/admin/exam-regulations";
    private const string Email = "b02h-personel@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task B02_OlusturmaIstegindeGecmeNotuYokken200_KaliciDeger0()
    {
        var (istemci, programId) = await KurAsync();

        var yanit = await istemci.PostAsJsonAsync(Uc, Govde(programId, null));

        Assert.Equal((0m, 0m), await SonucAsync(yanit));
    }

    // Karşı örnek: aynı istek, alan açık değerle.
    [Fact]
    public async Task B02_OlusturmaIstegindeGecmeNotu37ile200_KaliciDeger37()
    {
        var (istemci, programId) = await KurAsync();

        var yanit = await istemci.PostAsJsonAsync(Uc, Govde(programId, 37m));

        Assert.Equal((37m, 37m), await SonucAsync(yanit));
    }

    // Önceki değer 37 (karşı örnekteki istekle yazılır); güncelleme gövdesi aynı, yalnız alan yok.
    [Fact]
    public async Task B02_GuncellemeIstegindeGecmeNotuYokken200_KaliciDeger37den0a()
    {
        var (istemci, programId) = await KurAsync();
        var olusan = await istemci.PostAsJsonAsync(Uc, Govde(programId, 37m));
        Assert.Equal((37m, 37m), await SonucAsync(olusan));
        int id;
        await using (var db = fixture.YeniContext())
            id = await db.ExamRegulations.Select(r => r.Id).SingleAsync();

        var yanit = await istemci.PutAsJsonAsync($"{Uc}/{id}", Govde(programId, null));

        Assert.Equal((0m, 0m), await SonucAsync(yanit));
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

    private static Dictionary<string, object> Govde(int programId, decimal? gecmeNotu)
    {
        var govde = new Dictionary<string, object>
        {
            ["programId"] = programId, ["degreeLevel"] = "Bachelor", ["termType"] = "Fall", ["year"] = 2045, ["isActive"] = true,
            ["name"] = "Deneme Yönergesi", ["midtermCount"] = 1, ["finalCount"] = 1,
            ["minMidtermWeightPercent"] = 40, ["maxMidtermWeightPercent"] = 60, ["minFinalWeightPercent"] = 40, ["maxFinalWeightPercent"] = 60,
            ["rawScoreFloor1"] = 0, ["rawScoreFloor2"] = 0, ["finalExamFloor"] = 0,
        };
        if (gecmeNotu is { } deger)
            govde["passingGrade"] = deger;
        return govde;
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
