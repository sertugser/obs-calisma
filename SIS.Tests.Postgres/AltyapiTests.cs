using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIS.Domain.Entities;

namespace SIS.Tests.Postgres;

[Collection(PostgresKoleksiyonu.Ad)]
public sealed class AltyapiTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Duman testi: konteynere bağlanılıyor ve sunucu PostgreSQL 18.
    [Fact]
    public async Task PostgresSurumu_18()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        await using var komut = new NpgsqlCommand("show server_version_num", baglanti);
        var surum = int.Parse((string)(await komut.ExecuteScalarAsync())!);

        Assert.Equal(18, surum / 10000);
    }

    // O-01: derlemedeki bütün migration'lar boş veritabanına uygulanmış.
    [Fact]
    public async Task TumMigrationlar_Uygulanir()
    {
        await using var db = fixture.YeniContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
    }

    // O-01: model ile ModelSnapshot arasında fark yok.
    [Fact]
    public async Task Model_SnapshotIleFarksiz()
    {
        await using var db = fixture.YeniContext();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    // Temizlik test verisini siliyor; migration verisi olan tabloların satır sayısı migration sonrasıyla aynı.
    [Fact]
    public async Task Respawn_TestVerisiniSiler_MigrationVerisiniKorur()
    {
        await using (var db = fixture.YeniContext())
        {
            db.AuditLogs.Add(new AuditLog { Action = "Deneme", EntityType = "Deneme", Timestamp = DateTime.UtcNow });
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.AuditLogs.CountAsync());
        }

        await fixture.TemizleAsync();

        await using (var db = fixture.YeniContext())
        {
            Assert.Equal(0, await db.AuditLogs.CountAsync());
        }

        // Migration verisi: 6 tablo ve __EFMigrationsHistory.
        Assert.Equal(7, fixture.MigrationVerisi.Count);
        await using var baglanti = await fixture.BaglantiAcAsync();
        var sonra = await PostgresFixture.SatirSayilariAsync(baglanti);
        Assert.Equal(
            new SortedDictionary<string, long>(fixture.MigrationVerisi.ToDictionary()),
            new SortedDictionary<string, long>(fixture.MigrationVerisi.Keys.ToDictionary(t => t, t => sonra[t])));
    }
}
