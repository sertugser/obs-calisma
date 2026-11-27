using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence.Katalog;

namespace SIS.Tests.Postgres;

// Adım 3.3b: rol-izin kataloğu gerçek PostgreSQL'de (tekil dizinlerle).
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class RolIzinKataloguTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Simdi = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await fixture.TemizleAsync();
        await using var db = fixture.YeniContext();
        await SistemKatalogu.EsitleAsync(db, Simdi);
        await RolIzinKatalogu.EsitleAsync(db, Simdi);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TazeKurulum_ButunAtamalar_IkinciEsitlemeDegistirmez_KaldirilanGeriGelmez()
    {
        await using (var db = fixture.YeniContext())
        {
            Assert.Equal(RolIzinKatalogu.Kayitlar.Count, await db.RolePermissions.CountAsync());
            db.RolePermissions.Remove(await db.RolePermissions.OrderBy(rp => rp.Id).FirstAsync());
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.YeniContext())
        {
            var sonuc = await RolIzinKatalogu.EsitleAsync(db, Simdi);
            Assert.Equal(0, sonuc.Eklenen);
            Assert.Equal(1, sonuc.Atlanan);
        }

        await using var kontrol = fixture.YeniContext();
        Assert.Equal(RolIzinKatalogu.Kayitlar.Count - 1, await kontrol.RolePermissions.CountAsync());
    }
}
