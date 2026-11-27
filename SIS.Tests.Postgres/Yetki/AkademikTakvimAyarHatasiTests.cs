using System.Net;
using System.Text.Json;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Tests.Postgres.DersProgrami;

namespace SIS.Tests.Postgres.Yetki;

// Kurum saat dilimi ayarı bozukken öğrencinin akademik takvim ucu: tam HTTP boru hattı (hata ara katmanı dahil).
// Yanıt 400 kalır; gövdede kayıtlı değer ve yönetici ayar yolu yoktur, yalnız genel neden vardır.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class AkademikTakvimAyarHatasiTests(PostgresFixture fixture, DersProgramiUygulamasi uygulama)
    : IClassFixture<DersProgramiUygulamasi>, IAsyncLifetime
{
    private const string Email = "takvim-ogrenci@ornek.test";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OgrenciTakvimUcu_BozukAyarda400_GovdedeAyrintiYok()
    {
        var kurucu = new YetkiKurucu(fixture, uygulama.Al(fixture.BaglantiCumlesi));
        await kurucu.RolAsync("Student", "menu.akademik-takvim.view");
        await kurucu.KullaniciAsync(Email, "Student");
        int donemId;
        await using (var db = fixture.YeniContext())
        {
            var donem = db.AcademicTerms.Add(new AcademicTerm
            {
                Name = "2046-2047 Güz", ShortEnglishName = "Fall 2046", TermType = TermType.Fall, Year = 2046,
                TermStart = new DateTime(2046, 9, 15, 0, 0, 0, DateTimeKind.Utc), TermEnd = new DateTime(2047, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            }).Entity;
            db.SystemConfigs.Add(new SystemConfig { Key = "Institution.TimeZone", Value = "Mars/Olympus", DataType = "string" });
            await db.SaveChangesAsync();
            donemId = donem.Id;
        }

        var yanit = await (await kurucu.IstemciAsync(Email)).GetAsync($"/api/academic-calendar/{donemId}");

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        // Kaçışları çözülmüş bütün değerler: Türkçe harfler gövdede \u kaçışıyla gelir.
        using var json = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var degerler = string.Join(" ", json.RootElement.EnumerateObject().Select(p => p.Value.ToString()));
        Assert.DoesNotContain("Mars/Olympus", degerler);
        Assert.DoesNotContain("Sistem Ayarları", degerler);
        Assert.Equal("Kurum saat dilimi ayarı geçersiz olduğu için bu işlem şu anda yapılamıyor. Lütfen sistem yöneticisine başvurun.",
            json.RootElement.GetProperty("error").GetString());
    }
}
