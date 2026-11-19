using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace SIS.Tests.Postgres.DersProgrami;

// E-01: Excel içe aktarma önizlemesinde fakülte ve dönem kimliğinin nereden okunduğu (HTTP, model bağlama dahil).
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class E01_ExcelIceAktarmaParametreTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Onizleme = "/api/schedule-excel/import-preview";
    private const string Uygula = "/api/schedule-excel/import-apply";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Bilinen hata E-01: fakülte ve dönem yalnız form alanında gelince önizleme 500 dönüyor ve oturum yazılmıyor.
    [Fact]
    [Trait("Durum", "BilinenHata")]
    public async Task E01_FakulteVeDonemFormGovdesinde_Onizleme500_OturumYazilmaz()
    {
        var (veri, sube, istemci) = await KurAsync();
        using var form = Form(veri);
        form.Add(new StringContent(veri.FakulteId.ToString()), "facultyId");
        form.Add(new StringContent(veri.DonemId.ToString()), "termId");

        var onizleme = await istemci.PostAsync(Onizleme, form);

        Assert.Equal(HttpStatusCode.InternalServerError, onizleme.StatusCode);
        using var govde = JsonDocument.Parse(await onizleme.Content.ReadAsStringAsync());
        Assert.Equal("DbUpdateException", govde.RootElement.GetProperty("type").GetString());
        await using (var db = fixture.YeniContext())
            Assert.Equal(0, await db.ScheduleImportSessions.CountAsync());
        Assert.Empty(await veri.ProgramAsync(sube));
    }

    // Karşı örnek: aynı değerler sorgu dizesinde gelince okunur; satır hazırdır ve uygulama yazar.
    [Fact]
    public async Task E01_FakulteVeDonemSorguDizesinde_SatirHazir_UygulamaYazar()
    {
        var (veri, sube, istemci) = await KurAsync();
        using var form = Form(veri);

        var onizleme = await istemci.PostAsync($"{Onizleme}?termId={veri.DonemId}&facultyId={veri.FakulteId}", form);
        Assert.Equal(HttpStatusCode.OK, onizleme.StatusCode);
        var (oturum, satirlar) = await SatirlarAsync(onizleme);
        Assert.Equal(new[] { ("Valid", "Hazır.") }, satirlar);
        Assert.Equal((veri.FakulteId, veri.DonemId, false), await OturumAsync(oturum));

        var uygulamaYaniti = await istemci.PostAsJsonAsync(Uygula, Istek(veri, oturum));
        Assert.Equal(HttpStatusCode.OK, uygulamaYaniti.StatusCode);
        using var govde = JsonDocument.Parse(await uygulamaYaniti.Content.ReadAsStringAsync());
        Assert.Equal(1, govde.RootElement.GetProperty("appliedCount").GetInt32());
        Assert.Equal(new[] { ((int?)veri.D1Id, veri.Z1Id) }, await veri.ProgramAsync(sube));
        Assert.Equal((veri.FakulteId, veri.DonemId, true), await OturumAsync(oturum));
    }

    private async Task<(DersProgramiVeriKurucu, int, HttpClient)> KurAsync()
    {
        var veri = await DersProgramiVeriKurucu.KurAsync(fixture);
        Assert.NotEqual(veri.FakulteId, veri.DonemId);
        var sube = await veri.SubeEkleAsync(1);
        // Yazılan program satırındaki kimlikler (şube, zaman dilimi, derslik) denk gelirse yer değiştirme fark edilmez.
        Assert.Equal(5, new[] { sube, veri.Z1Id, veri.Z2Id, veri.D1Id, veri.D2Id }.Distinct().Count());
        return (veri, sube, await DersProgramiVeriKurucu.YoneticiIstemcisiAsync(uygulama.Al(fixture.BaglantiCumlesi)));
    }

    private static object Istek(DersProgramiVeriKurucu veri, Guid oturum) =>
        new { facultyId = veri.FakulteId, termId = veri.DonemId, importSessionId = oturum, rowNumbers = new[] { 2 } };

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

    private static async Task<(Guid, (string, string)[])> SatirlarAsync(HttpResponseMessage yanit)
    {
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var kok = govde.RootElement;
        var satirlar = kok.GetProperty("rows").EnumerateArray()
            .Select(s => (s.GetProperty("status").GetString()!, s.GetProperty("message").GetString()!)).ToArray();
        Assert.Equal(satirlar.Length, kok.GetProperty("totalRows").GetInt32());
        return (kok.GetProperty("importSessionId").GetGuid(), satirlar);
    }

    // Oturumun kalıcı fakülte, dönem ve tüketilme durumu, yeni bir context'le.
    private async Task<(int, int, bool)> OturumAsync(Guid oturum)
    {
        await using var db = fixture.YeniContext();
        var o = await db.ScheduleImportSessions.SingleAsync(s => s.Id == oturum);
        return (o.FacultyId, o.TermId, o.ConsumedAt.HasValue);
    }
}
