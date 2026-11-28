using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence.Kurulum;
using SIS.Tests.Postgres.NotHesabi;

namespace SIS.Tests.Postgres;

// Adım 3.10: tek başlangıç migration'ı, eski zincirden geçiş ve üç eksik kısıt.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class BaslangicMigrationTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Uçtan uca: eski zincirin sonundaki veritabanı (EksikKisitlar öncesi şema, 151 satırlık geçmiş) geçişten
    // sonra olağan MigrateAsync ile EksikKisitlar'ı alır.
    [Fact]
    public async Task EskiZincirinSonundakiVeritabani_GecisVeMigrate_UcKisitEklenir()
    {
        var onceki = await GecmisAsync();
        try
        {
            await EskiSemaya_DonAsync();

            await using (var db = fixture.YeniContext())
            {
                Assert.True(await EskiZincirGecisi.UygulaAsync(db));
                Assert.Equal(new[] { EskiZincirGecisi.Baslangic }, await GecmisAsync());
                await db.Database.MigrateAsync();
            }

            Assert.Equal(onceki, await GecmisAsync());
            Assert.Equal(3, await KisitSayisiAsync());
            await using (var db = fixture.YeniContext())
                Assert.False(await EskiZincirGecisi.UygulaAsync(db)); // ikinci çağrı hiçbir şey yapmaz
        }
        finally
        {
            if (await KisitSayisiAsync() < 3)
            {
                await GecmisYazAsync([EskiZincirGecisi.Baslangic]);
                await using var db = fixture.YeniContext();
                await db.Database.MigrateAsync();
            }
            await GecmisYazAsync(onceki);
        }
    }

    // Eski veritabanında kısıta aykırı veri varsa EksikKisitlar düşer; geçmiş Baslangic'te kalır, veri
    // düzeltilince yeniden çalıştırma tamamlar.
    [Fact]
    public async Task EskiVeritabanindaAykiriPuan_EksikKisitlarDuser_DuzeltinceTamamlanir()
    {
        var onceki = await GecmisAsync();
        try
        {
            await EskiSemaya_DonAsync();
            var veri = await NotVeriKurucu.KurAsync(fixture);
            var sube = await veri.SubeEkleAsync(hoca: 0, no: 1);
            var bilesen = await veri.BilesenEkleAsync("Vize", 40m, GradeComponentType.Midterm, 1);
            var (_, kayit) = await veri.OgrenciEkleAsync(sube, "01");
            await veri.PuanEkleAsync(kayit, bilesen, 120m);

            await using (var db = fixture.YeniContext())
            {
                Assert.True(await EskiZincirGecisi.UygulaAsync(db));
                var hata = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
                Assert.Contains("CK_StudentGrade_Score_Aralik", hata.ToString());
            }
            Assert.Equal(new[] { EskiZincirGecisi.Baslangic }, await GecmisAsync());

            await using (var db = fixture.YeniContext())
            {
                await db.StudentGrades.ExecuteUpdateAsync(g => g.SetProperty(x => x.Score, 100m));
                Assert.False(await EskiZincirGecisi.UygulaAsync(db));
                await db.Database.MigrateAsync();
            }
            Assert.Equal(onceki, await GecmisAsync());
            Assert.Equal(3, await KisitSayisiAsync());
        }
        finally
        {
            if (await KisitSayisiAsync() < 3)
            {
                await using var db = fixture.YeniContext();
                await db.StudentGrades.ExecuteDeleteAsync();
                await GecmisYazAsync([EskiZincirGecisi.Baslangic]);
                await db.Database.MigrateAsync();
            }
            await GecmisYazAsync(onceki);
        }
    }

    [Fact]
    public async Task EskiZincirinOrtasindakiVeritabani_Reddedilir_GecmisDegismez()
    {
        var onceki = await GecmisAsync();
        try
        {
            var yarim = EskiZincir().SkipLast(1).ToArray();
            await GecmisYazAsync(yarim);

            await using (var db = fixture.YeniContext())
                await Assert.ThrowsAsync<InvalidOperationException>(() => EskiZincirGecisi.UygulaAsync(db));

            Assert.Equal(yarim, await GecmisAsync());
        }
        finally
        {
            await GecmisYazAsync(onceki);
        }
    }

    [Fact]
    public async Task IkinciEtkinDonem_Reddedilir_AyniKayittaDegistirmeKabul()
    {
        var veri = await NotVeriKurucu.KurAsync(fixture); // etkin bir dönem kurar
        await using (var db = fixture.YeniContext())
        {
            db.AcademicTerms.Add(Donem(2025, etkin: true));
            Assert.Equal("EX_AcademicTerms_TekEtkinDonem", KisitAdi(await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync())));
        }

        // Eskiyi kapatıp yeniyi açan tek kayıt: denetim işlem sonunda yapıldığı için satır sırası önemsiz.
        await using (var db = fixture.YeniContext())
        {
            var yeni = Donem(2025, etkin: false);
            db.AcademicTerms.Add(yeni);
            await db.SaveChangesAsync();
            (await db.AcademicTerms.SingleAsync(t => t.Id == veri.DonemId)).IsActivated = false;
            yeni.IsActivated = true;
            await db.SaveChangesAsync();
        }

        // İptal edilmiş dönem sayılmaz.
        await using (var db = fixture.YeniContext())
        {
            db.AcademicTerms.Add(Donem(2026, etkin: true, iptal: true));
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.AcademicTerms.CountAsync(t => t.IsActivated && !t.IsCancelled));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public async Task AralikDisiPuan_Reddedilir(decimal puan)
    {
        var veri = await NotVeriKurucu.KurAsync(fixture);
        var sube = await veri.SubeEkleAsync(hoca: 0, no: 1);
        var bilesen = await veri.BilesenEkleAsync("Vize", 40m, GradeComponentType.Midterm, 1);
        var (_, kayit) = await veri.OgrenciEkleAsync(sube, "01");

        var hata = await Assert.ThrowsAnyAsync<Exception>(() => veri.PuanEkleAsync(kayit, bilesen, puan));
        Assert.Equal("CK_StudentGrade_Score_Aralik", KisitAdi(hata));
        await veri.PuanEkleAsync(kayit, bilesen, 100m); // sınır değeri kabul
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public async Task AralikDisiAgirlik_Reddedilir(decimal agirlik)
    {
        var veri = await NotVeriKurucu.KurAsync(fixture);

        var hata = await Assert.ThrowsAnyAsync<Exception>(
            () => veri.BilesenEkleAsync("Vize", agirlik, GradeComponentType.Midterm, 1));
        Assert.Equal("CK_GradeComponent_Weight_Aralik", KisitAdi(hata));
        await veri.BilesenEkleAsync("Final", 100m, GradeComponentType.Final, 2);
    }

    // Ertelemeli kısıt kayıtta değil işlem sonunda düşer; hata DbUpdateException içinde ya da doğrudan gelebilir.
    private static string? KisitAdi(Exception hata)
    {
        for (Exception? e = hata; e is not null; e = e.InnerException)
            if (e is PostgresException pg) return pg.ConstraintName;
        return null;
    }

    private static AcademicTerm Donem(int yil, bool etkin, bool iptal = false) => new()
    {
        Name = $"{yil}-{yil + 1} Bahar", ShortEnglishName = $"Spring {yil}", TermType = TermType.Spring, Year = yil,
        IsActivated = etkin, IsCancelled = iptal,
        TermStart = new DateTime(yil + 1, 2, 15, 0, 0, 0, DateTimeKind.Utc),
        TermEnd = new DateTime(yil + 1, 6, 10, 0, 0, 0, DateTimeKind.Utc),
    };

    // EksikKisitlar'ın üç kısıtı kaldırılır ve geçmiş, eski zincirin 151 satırıyla değiştirilir.
    private async Task EskiSemaya_DonAsync()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        await using (var komut = new NpgsqlCommand("""
            ALTER TABLE "AcademicTerms" DROP CONSTRAINT "EX_AcademicTerms_TekEtkinDonem";
            ALTER TABLE "StudentGrades" DROP CONSTRAINT "CK_StudentGrade_Score_Aralik";
            ALTER TABLE "GradeComponents" DROP CONSTRAINT "CK_GradeComponent_Weight_Aralik";
            """, baglanti))
            await komut.ExecuteNonQueryAsync();
        await GecmisYazAsync(EskiZincir());
    }

    // Eski zincirin biçiminde 151 kimlik; sonuncusu gerçek son migration.
    private static string[] EskiZincir() =>
        [.. Enumerable.Range(1, EskiZincirGecisi.EskiZincirUzunlugu - 1).Select(i => $"2026010100{i:0000}_Eski{i}"),
         EskiZincirGecisi.EskiZincirinSonu];

    private async Task<long> KisitSayisiAsync()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        await using var komut = new NpgsqlCommand("""
            select count(*) from pg_constraint where conname in
              ('EX_AcademicTerms_TekEtkinDonem', 'CK_StudentGrade_Score_Aralik', 'CK_GradeComponent_Weight_Aralik')
            """, baglanti);
        return (long)(await komut.ExecuteScalarAsync())!;
    }

    private async Task<string[]> GecmisAsync()
    {
        await using var db = fixture.YeniContext();
        return (await db.Database.GetAppliedMigrationsAsync()).ToArray();
    }

    // Geçmiş tablosunu verilen satırlarla değiştirir (Respawn bu tabloya dokunmaz; test sonunda geri yazılır).
    private async Task GecmisYazAsync(IReadOnlyList<string> kimlikler)
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        await using var islem = await baglanti.BeginTransactionAsync();
        await using (var sil = new NpgsqlCommand("""DELETE FROM "__EFMigrationsHistory";""", baglanti, islem))
            await sil.ExecuteNonQueryAsync();
        foreach (var kimlik in kimlikler)
        {
            await using var ekle = new NpgsqlCommand(
                """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES (@k, '10.0.0');""", baglanti, islem);
            ekle.Parameters.AddWithValue("k", kimlik);
            await ekle.ExecuteNonQueryAsync();
        }
        await islem.CommitAsync();
    }
}
