using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Postgres;

// Kurum saat dilimi ayarı çekirdek tohumda: yoksa varsayılanla (Etc/UTC) eklenir, varsa değeri korunur.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class KurumSaatDilimiTohumTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string?> TohumlaVeOkuAsync(string? onceYazilacak = null)
    {
        await using (var db = fixture.YeniContext())
        {
            if (onceYazilacak is not null)
                (await db.SystemConfigs.SingleAsync(c => c.Key == "Institution.TimeZone")).Value = onceYazilacak;
            await db.SaveChangesAsync();
            await SeedData.SeedSystemConfigAsync(db);
        }
        await using var kontrol = fixture.YeniContext();
        return await kontrol.SystemConfigs.Where(c => c.Key == "Institution.TimeZone").Select(c => c.Value).SingleOrDefaultAsync();
    }

    [Fact]
    public async Task BosVeritabaninda_VarsayilanlaEklenir_VarOlanDegerIkinciTohumdaKorunur()
    {
        await using (var db = fixture.YeniContext())
            Assert.False(await db.SystemConfigs.AnyAsync(c => c.Key == "Institution.TimeZone"));

        Assert.Equal("Etc/UTC", await TohumlaVeOkuAsync());
        Assert.Equal("Europe/Istanbul", await TohumlaVeOkuAsync(onceYazilacak: "Europe/Istanbul"));
    }
}
