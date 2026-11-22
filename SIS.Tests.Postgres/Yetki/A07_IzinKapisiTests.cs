using System.Net;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// A-07: yönetim izni isteyen uç (dönem iptali). İki test arasındaki tek fark, rolün iptal izni satırıdır.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class A07_IzinKapisiTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "a07-ogrenci@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A07_OgrenciRoluDonemIptalinde403Alir_DonemlerDegismez()
    {
        var (istemci, hedef, _) = await KurAsync();

        var yanit = await istemci.PostAsync($"/api/admin/terms/{hedef}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        Assert.Empty(await IptalEdilenlerAsync());
    }

    // Karşı örnek: aynı rol ve aynı kurulum; rolde "menu.akademik-donem.cancel" izin satırı var.
    [Fact]
    public async Task A07_IptalIzniOlanRolDonemIptalinde200Alir_YalnizHedefDonemIptal()
    {
        var (istemci, hedef, digeri) = await KurAsync("menu.akademik-donem.cancel");

        var yanit = await istemci.PostAsync($"/api/admin/terms/{hedef}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal([hedef], await IptalEdilenlerAsync());
        Assert.NotEqual(hedef, digeri);
    }

    // Rolde yalnız dönem görüntüleme izni var; karşı örnek yukarıdaki iptal izni olan rol testi.
    [Fact]
    public async Task A07_YalnizDonemGoruntulemeIzniOlanRolDonemIptalinde403Alir_GovdeBos_DonemlerDegismez()
    {
        var (istemci, hedef, _) = await KurAsync("menu.akademik-donem.view");

        var yanit = await istemci.PostAsync($"/api/admin/terms/{hedef}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        Assert.Equal(string.Empty, await yanit.Content.ReadAsStringAsync());
        Assert.Empty(await IptalEdilenlerAsync());
    }

    /// <summary>
    /// Öğrenci rolü ve kullanıcısı; iptal izni her iki testte de başka bir rolde var (kullanıcısız).
    /// Hedef ve ikinci bir dönem (migration'daki dönemler de yerinde); dönem kimlikleri kullanıcı ve rol kimliğiyle denk gelmez.
    /// </summary>
    private async Task<(HttpClient, int Hedef, int Digeri)> KurAsync(params string[] izinler)
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        var kimlikler = new HashSet<int>
        {
            await kurucu.RolAsync("Dean", "menu.akademik-donem.cancel"),
            await kurucu.RolAsync("Student", izinler),
        };
        kimlikler.Add(await kurucu.KullaniciAsync(Email, "Student"));
        var donemler = new List<int>();
        for (var yil = 2041; donemler.Count < 2; yil++)
        {
            var id = await DonemEkleAsync(yil);
            if (!kimlikler.Contains(id))
                donemler.Add(id);
        }

        Assert.Empty(await IptalEdilenlerAsync());
        return (await kurucu.IstemciAsync(Email), donemler[0], donemler[1]);
    }

    private async Task<int> DonemEkleAsync(int yil)
    {
        await using var db = fixture.YeniContext();
        var donem = new AcademicTerm
        {
            Name = $"{yil}-{yil + 1} Güz", ShortEnglishName = $"Fall {yil}", TermType = TermType.Fall, Year = yil,
            TermStart = new DateTime(yil, 9, 15, 0, 0, 0, DateTimeKind.Utc), TermEnd = new DateTime(yil + 1, 1, 15, 0, 0, 0, DateTimeKind.Utc)
        };
        db.AcademicTerms.Add(donem);
        await db.SaveChangesAsync();
        return donem.Id;
    }

    private async Task<List<int>> IptalEdilenlerAsync()
    {
        await using var db = fixture.YeniContext();
        return await db.AcademicTerms.AsNoTracking().Where(t => t.IsCancelled).OrderBy(t => t.Id).Select(t => t.Id).ToListAsync();
    }
}
