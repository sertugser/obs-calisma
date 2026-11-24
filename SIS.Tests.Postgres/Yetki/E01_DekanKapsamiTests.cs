using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// E-01, dekan kısmı: ders programı Excel önizlemesi dekan rolüyle. İstek, E01_ExcelIceAktarmaParametreTests'teki kabul testinin
// biçiminde (fakülte ve dönem sorgu dizesinde); iki test arasındaki tek fark sorgudaki fakülte kimliği.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class E01_DekanKapsamiTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Onizleme = "/api/schedule-excel/import-preview";
    private const string Email = "e01-dekan@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task E01_DekanKendiFakultesindeOnizleme200Alir_SatirHazir_OturumKendiFakultesinde()
    {
        var (veri, _, istemci) = await KurAsync();
        using var form = Form(veri);

        var yanit = await istemci.PostAsync($"{Onizleme}?termId={veri.DonemId}&facultyId={veri.FakulteId}", form);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var kok = govde.RootElement;
        var satirlar = kok.GetProperty("rows").EnumerateArray()
            .Select(s => (s.GetProperty("status").GetString(), s.GetProperty("message").GetString())).ToArray();
        Assert.Equal(new (string?, string?)[] { ("Valid", "Hazır.") }, satirlar);
        Assert.Equal(1, kok.GetProperty("totalRows").GetInt32());
        var oturum = kok.GetProperty("importSessionId").GetGuid();
        await using var db = fixture.YeniContext();
        var o = await db.ScheduleImportSessions.SingleAsync();
        Assert.Equal((oturum, veri.FakulteId, veri.DonemId, false), (o.Id, o.FacultyId, o.TermId, o.ConsumedAt.HasValue));
    }

    // Karşı örnek: aynı dekan, aynı dosya ve dönem; sorguda başka bir fakültenin kimliği.
    [Fact]
    public async Task E01_DekanBaskaFakultedeOnizleme403Alir_GovdeBos_OturumYazilmaz()
    {
        var (veri, baska, istemci) = await KurAsync();
        using var form = Form(veri);

        var yanit = await istemci.PostAsync($"{Onizleme}?termId={veri.DonemId}&facultyId={baska}", form);

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        Assert.Equal(string.Empty, await yanit.Content.ReadAsStringAsync());
        await using var db = fixture.YeniContext();
        Assert.Equal(0, await db.ScheduleImportSessions.CountAsync());
    }

    /// <summary>
    /// Ders programı verisi ve şube 1 fakülte A'da; dekan A'nın ikinci bölümünde öğretim elemanı (dersin bölümünden farklı) ve
    /// UserScopes satırı A. Başka fakülte B bölümsüz. Fakülte, dekanın bölümü, öğretim elemanı ve dönem kimlikleri ayrık (ön koşul).
    /// </summary>
    private async Task<(DersProgramiVeriKurucu, int Baska, HttpClient)> KurAsync()
    {
        var veri = await DersProgramiVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(1);
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("Dean", "menu.ders-programi-olusturma.import");
        var kullanici = await kurucu.KullaniciAsync(Email, "Dean");
        int dersBolumu;
        await using (var db = fixture.YeniContext())
            dersBolumu = await db.Departments.Where(d => d.FacultyId == veri.FakulteId).Select(d => d.Id).SingleAsync();
        var ayrik = KapsamVeriKurucu.Ayristirici(fixture, veri.FakulteId, dersBolumu, veri.DonemId, kullanici, veri.HocaId, sube);
        var dekanBolumu = await ayrik.BolumAsync(veri.FakulteId, "DKB");
        var baska = await ayrik.FakulteAsync("IKF");
        var dekan = await ayrik.OgretimElemaniAsync(kullanici, dekanBolumu);
        await kurucu.FakulteKapsamiAsync(kullanici, veri.FakulteId);
        Assert.Distinct([veri.FakulteId, baska, dekanBolumu, dekan, veri.DonemId]);
        Assert.NotEqual(dersBolumu, dekanBolumu);
        return (veri, baska, await kurucu.IstemciAsync(Email));
    }

    // Tek veri satırlı dosya (satır 2): ders, şube 1, Pazartesi Z1, derslik D1.
    private static MultipartFormDataContent Form(DersProgramiVeriKurucu veri)
    {
        using var kitap = new XLWorkbook();
        var sayfa = kitap.AddWorksheet("Program Verisi");
        string[] baslik = ["Ders Kodu", "Ders Adı", "Şube", "Öğretim Üyesi", "Gün", "Başlangıç", "Bitiş", "Derslik", "Kapasite"];
        string[] satir = [DersProgramiVeriKurucu.DersKodu, "", "1", "", "Pazartesi", "09:00", "09:50", veri.D1Adi, ""];
        for (var i = 0; i < baslik.Length; i++)
        {
            sayfa.Cell(1, i + 1).Value = baslik[i];
            sayfa.Cell(2, i + 1).SetValue(satir[i]);
        }
        using var akis = new MemoryStream();
        kitap.SaveAs(akis);
        var dosya = new ByteArrayContent(akis.ToArray());
        dosya.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { dosya, "file", "program.xlsx" } };
    }
}
