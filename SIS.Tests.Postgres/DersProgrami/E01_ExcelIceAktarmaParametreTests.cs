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

    // Ön yüz sözleşmesi: fakülte ve dönem form alanında gelir; satır hazırdır ve uygulama yazar.
    [Fact]
    public async Task E01_FakulteVeDonemFormGovdesinde_SatirHazir_UygulamaYazar()
    {
        var (veri, sube, istemci) = await KurAsync();
        using var form = Form(veri, veri.FakulteId.ToString(), veri.DonemId.ToString());

        var onizleme = await istemci.PostAsync(Onizleme, form);
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

    // Karşı örnek: aynı değerler yalnız sorgu dizesinde gelince okunmaz.
    [Fact]
    public async Task E01_FakulteVeDonemYalnizSorguDizesinde_400_OturumYazilmaz()
    {
        var (veri, sube, istemci) = await KurAsync();
        using var form = Form(veri, null, null);

        var onizleme = await istemci.PostAsync($"{Onizleme}?termId={veri.DonemId}&facultyId={veri.FakulteId}", form);

        await RetAsync(onizleme, EksikIleti);
        Assert.Empty(await veri.ProgramAsync(sube));
    }

    [Fact]
    public async Task E01_FormAlanlariYok_400_OturumYazilmaz()
    {
        var (veri, _, istemci) = await KurAsync();
        using var form = Form(veri, null, null);

        await RetAsync(await istemci.PostAsync(Onizleme, form), EksikIleti);
    }

    // Sayı olmayan fakülte: model bağlama hatası, alan adıyla.
    [Fact]
    public async Task E01_FakulteSayiDegil_400_OturumYazilmaz()
    {
        var (veri, _, istemci) = await KurAsync();
        using var form = Form(veri, "abc", veri.DonemId.ToString());

        var yanit = await istemci.PostAsync(Onizleme, form);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var alanlar = govde.RootElement.GetProperty("errors").EnumerateObject().Select(a => a.Name).ToArray();
        Assert.Equal(new[] { "facultyId" }, alanlar);
        await OturumYokAsync();
    }

    [Fact]
    public async Task E01_YoneticiVarOlmayanFakulte_400_OturumYazilmaz()
    {
        var (veri, _, istemci) = await KurAsync();
        using var form = Form(veri, (await YokKimlikAsync()).ToString(), veri.DonemId.ToString());

        await RetAsync(await istemci.PostAsync(Onizleme, form), "Fakülte bulunamadı.");
    }

    [Fact]
    public async Task E01_YoneticiVarOlmayanDonem_400_OturumYazilmaz()
    {
        var (veri, _, istemci) = await KurAsync();
        using var form = Form(veri, veri.FakulteId.ToString(), (await YokKimlikAsync()).ToString());

        await RetAsync(await istemci.PostAsync(Onizleme, form), "Dönem bulunamadı.");
    }

    private const string EksikIleti = "Fakülte ve dönem gönderilmelidir.";

    // 400, beklenen { error } iletisi ve yazılmamış oturum (yeni bir context'le).
    private async Task RetAsync(HttpResponseMessage yanit, string ileti)
    {
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal(ileti, govde.RootElement.GetProperty("error").GetString());
        await OturumYokAsync();
    }

    private async Task OturumYokAsync()
    {
        await using var db = fixture.YeniContext();
        Assert.Equal(0, await db.ScheduleImportSessions.CountAsync());
    }

    // Hiçbir fakültenin ve dönemin kimliği olmayan bir kimlik.
    private async Task<int> YokKimlikAsync()
    {
        await using var db = fixture.YeniContext();
        var enBuyuk = Math.Max(await db.Faculties.MaxAsync(f => f.Id), await db.AcademicTerms.MaxAsync(t => t.Id));
        return enBuyuk + 1000;
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

    // Tek veri satırlı dosya (satır 2): ders, şube 1, Pazartesi Z1, derslik D1; null olan form alanı eklenmez.
    private static MultipartFormDataContent Form(DersProgramiVeriKurucu veri, string? fakulte, string? donem)
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
        var form = new MultipartFormDataContent { { dosya, "file", "program.xlsx" } };
        if (fakulte is not null)
            form.Add(new StringContent(fakulte), "facultyId");
        if (donem is not null)
            form.Add(new StringContent(donem), "termId");
        return form;
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
