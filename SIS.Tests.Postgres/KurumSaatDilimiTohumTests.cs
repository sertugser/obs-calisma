using Microsoft.EntityFrameworkCore;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Postgres;

// Kurum saat dilimi ayarı çekirdek tohumda: yoksa varsayılanla (Etc/UTC) eklenir, varsa değeri korunur.
// Tohum, anahtarın bu koşuda ilk kez yazılıp yazılmadığını döner; örnek veri tohumu İstanbul'u yalnız o zaman atar.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class KurumSaatDilimiTohumTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Anahtar = "Institution.TimeZone";

    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string?> TohumlaVeOkuAsync(string? onceYazilacak = null)
    {
        await using (var db = fixture.YeniContext())
        {
            if (onceYazilacak is not null)
                (await db.SystemConfigs.SingleAsync(c => c.Key == Anahtar)).Value = onceYazilacak;
            await db.SaveChangesAsync();
            await SeedData.SeedSystemConfigAsync(db);
        }
        return await OkuAsync();
    }

    private async Task<string?> OkuAsync()
    {
        await using var kontrol = fixture.YeniContext();
        return await kontrol.SystemConfigs.Where(c => c.Key == Anahtar).Select(c => c.Value).SingleOrDefaultAsync();
    }

    private async Task<bool> TohumlaAsync()
    {
        await using var db = fixture.YeniContext();
        return await SeedData.SeedSystemConfigAsync(db);
    }

    [Fact]
    public async Task BosVeritabaninda_VarsayilanlaEklenir_VarOlanDegerIkinciTohumdaKorunur()
    {
        await using (var db = fixture.YeniContext())
            Assert.False(await db.SystemConfigs.AnyAsync(c => c.Key == Anahtar));

        Assert.Equal("Etc/UTC", await TohumlaVeOkuAsync());
        Assert.Equal("Europe/Istanbul", await TohumlaVeOkuAsync(onceYazilacak: "Europe/Istanbul"));
    }

    [Fact]
    public async Task BosVeritabaninda_BayrakTrue_IkinciTohumdaFalse()
    {
        Assert.True(await TohumlaAsync());
        Assert.Equal("Etc/UTC", await OkuAsync());

        Assert.False(await TohumlaAsync());
        Assert.Equal("Etc/UTC", await OkuAsync());
    }

    [Fact]
    public async Task AnahtarVarken_BayrakFalse_DegerDegismez()
    {
        await using (var db = fixture.YeniContext())
        {
            db.SystemConfigs.Add(new() { Key = Anahtar, Value = "Europe/Berlin", Category = "Localization", DataType = "string" });
            await db.SaveChangesAsync();
        }

        // Anahtar katalogdan önce var: katalog onu yalnız kayda alır.
        Assert.False(await TohumlaAsync());
        Assert.Equal("Europe/Berlin", await OkuAsync());
    }

    [Fact]
    public async Task YoneticiAnahtariSildiyse_GeriGelmez_BayrakFalse()
    {
        Assert.True(await TohumlaAsync());
        await using (var db = fixture.YeniContext())
        {
            db.SystemConfigs.Remove(await db.SystemConfigs.SingleAsync(c => c.Key == Anahtar));
            await db.SaveChangesAsync();
        }

        Assert.False(await TohumlaAsync());
        Assert.Null(await OkuAsync());
    }

    [Fact]
    public async Task SistemTohumu_BayragiDondurur()
    {
        await using (var db = fixture.YeniContext())
            Assert.True(await SeedData.SeedSystemAsync(db, roleManager: null!)); // rol yöneticisi kullanılmıyor
        await using (var db = fixture.YeniContext())
            Assert.False(await SeedData.SeedSystemAsync(db, roleManager: null!));
    }
}
