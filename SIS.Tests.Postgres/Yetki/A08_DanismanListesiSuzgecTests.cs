using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Enums;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// A-08 (1) ek: danışan listesinde öğrenci durumu ve program satırı. D1'in üç danışanı: X etkin; P'nin durumu Passive (programı etkin);
// Q etkin, ana program satırı etkin değil.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class A08_DanismanListesiSuzgecTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "a08s-danisman@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A08_DanismanListesi200Alir_YalnizDurumuVeProgramiEtkinDanisanDoner()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("AcademicAdvisor", "menu.ders-kayit-onay.view");
        var dKullanici = await kurucu.KullaniciAsync(Email, "AcademicAdvisor");
        var xKullanici = await kurucu.KullaniciAsync("a08s-ogrenci-x@ornek.test");
        var pKullanici = await kurucu.KullaniciAsync("a08s-ogrenci-p@ornek.test");
        var qKullanici = await kurucu.KullaniciAsync("a08s-ogrenci-q@ornek.test");
        var veri = await KapsamVeriKurucu.KurAsync(fixture, dKullanici, xKullanici, pKullanici, qKullanici);
        var d = await veri.OgretimElemaniAsync(dKullanici);
        var x = await veri.OgrenciAsync(xKullanici, d);
        var p = await veri.OgrenciAsync(pKullanici, d);
        var q = await veri.OgrenciAsync(qKullanici, d);
        Assert.Distinct([dKullanici, xKullanici, pKullanici, qKullanici, d, x, p, q]);
        await using (var db = fixture.YeniContext())
        {
            await db.Students.Where(s => s.Id == p).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, StudentStatus.Passive));
            await db.StudentPrograms.Where(sp => sp.StudentId == q).ExecuteUpdateAsync(s => s.SetProperty(sp => sp.IsActive, false));
            // Kurulum: üçü de D1'in danışanı; P yalnız durumla, Q yalnız program satırıyla X'ten ayrılıyor.
            var durum = await db.Students.Where(s => s.AdvisorId == d).OrderBy(s => s.Id)
                .Select(s => new { s.Id, s.Status, Etkin = s.StudentPrograms.Single().IsActive }).ToListAsync();
            Assert.Equal(
                new[] { (x, StudentStatus.Active, true), (p, StudentStatus.Passive, true), (q, StudentStatus.Active, false) }.OrderBy(t => t.Item1),
                durum.Select(s => (s.Id, s.Status, s.Etkin)));
        }
        var istemci = await kurucu.IstemciAsync(Email);

        var yanit = await istemci.GetAsync("/api/advisor/students");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var govde = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal([x], govde.RootElement.EnumerateArray().Select(s => s.GetProperty("studentId").GetInt32()).ToList());
    }
}
