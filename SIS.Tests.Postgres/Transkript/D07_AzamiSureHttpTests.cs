using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Tests.Postgres.DersProgrami;
using SIS.Tests.Postgres.Yetki;

namespace SIS.Tests.Postgres.Transkript;

// D-07 (HTTP): program oluşturma ve güncelleme isteğinde azami öğrenim süresi alanının eşlemesi. Servis düzeyi (gelen değer olduğu
// gibi yazılıyor) SIS.Tests'teki D07_AzamiSureTests ile sabit. İstekler arasındaki tek fark maxStudyDurationSemesters alanı (güncellemede
// bir de ad). D37: alan gönderilmezse 0 yazılmıyor, istek 400 ile reddediliyor.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class D07_AzamiSureHttpTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Uc = "/api/admin/academic-structure/programs";
    private const string Email = "d07h-personel@ornek.test";
    private const string Kod = "HTP";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task D07_OlusturmaIstegindeAzamiSureYokken400_KayitYok()
    {
        var (istemci, bolumId) = await KurAsync();

        var yanit = await istemci.PostAsJsonAsync(Uc, Govde(bolumId, null));

        await HataAsync(yanit);
        await using var db = fixture.YeniContext();
        Assert.False(await db.AcademicPrograms.AnyAsync(p => p.Code == Kod));
    }

    // Karşı örnek: aynı istek, alan açık değerle (11; seed'deki 14'ten ve formdaki 8'den farklı).
    [Fact]
    public async Task D07_OlusturmaIstegindeAzamiSure11ile200_KaliciDeger11()
    {
        var (istemci, bolumId) = await KurAsync();

        var yanit = await istemci.PostAsJsonAsync(Uc, Govde(bolumId, 11));

        Assert.Equal((11, 11), await SonucAsync(yanit));
    }

    // Önceki değer 11 (karşı örnekteki istekle yazılır); güncelleme gövdesinde alan yok ve ad farklı: hiçbir alan yazılmamalı.
    [Fact]
    public async Task D07_GuncellemeIstegindeAzamiSureYokken400_KaliciDeger11Kalir()
    {
        var (istemci, bolumId) = await KurAsync();
        var olusan = await istemci.PostAsJsonAsync(Uc, Govde(bolumId, 11));
        Assert.Equal((11, 11), await SonucAsync(olusan));
        int id;
        await using (var db = fixture.YeniContext())
            id = await db.AcademicPrograms.Where(p => p.Code == Kod).Select(p => p.Id).SingleAsync();

        var yanit = await istemci.PutAsJsonAsync($"{Uc}/{id}", Govde(bolumId, null, "Başka Program"));

        await HataAsync(yanit);
        await using var yeni = fixture.YeniContext();
        var kalici = await yeni.AcademicPrograms.AsNoTracking().Where(p => p.Id == id).Select(p => new { p.MaxStudyDurationSemesters, p.Name }).SingleAsync();
        Assert.Equal((11, "Deneme Programı"), (kalici.MaxStudyDurationSemesters, kalici.Name));
    }

    /// <summary>Program oluşturma ve düzenleme izinli rol (sistem yöneticisi değil); KapsamVeriKurucu'nun bölümü.</summary>
    private async Task<(HttpClient, int BolumId)> KurAsync()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("AdministrativeStaff", "menu.program-tanimlari.create", "menu.program-tanimlari.edit");
        var kullanici = await kurucu.KullaniciAsync(Email, "AdministrativeStaff");
        var veri = await KapsamVeriKurucu.KurAsync(fixture, kullanici);
        return (await kurucu.IstemciAsync(Email), veri.BolumId);
    }

    private static Dictionary<string, object?> Govde(int bolumId, int? azamiSure, string ad = "Deneme Programı")
    {
        var govde = new Dictionary<string, object?>
        {
            ["name"] = ad, ["code"] = Kod, ["externalSystemCode"] = null, ["departmentId"] = bolumId,
            ["degreeLevel"] = "Bachelor", ["educationType"] = "Semester", ["totalRequiredEcts"] = 240,
        };
        if (azamiSure is { } deger)
            govde["maxStudyDurationSemesters"] = deger;
        return govde;
    }

    // 400 iddiası; gövdede tek hata iletisi.
    private static async Task HataAsync(HttpResponseMessage yanit)
    {
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal("Azami öğrenim süresi gönderilmelidir.", govde.RootElement.GetProperty("message").GetString());
    }

    // 200 iddiası; dönen azami süre ve programın kalıcı azami süresi (yeni bir context'le).
    private async Task<(int Donen, int Kalici)> SonucAsync(HttpResponseMessage yanit)
    {
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var id = govde.RootElement.GetProperty("id").GetInt32();
        await using var db = fixture.YeniContext();
        var kalici = await db.AcademicPrograms.AsNoTracking().Where(p => p.Id == id).Select(p => p.MaxStudyDurationSemesters).SingleAsync();
        return (govde.RootElement.GetProperty("maxStudyDurationSemesters").GetInt32(), kalici);
    }
}
