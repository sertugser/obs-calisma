using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Postgres.DersKaydi;

// O-04: denetim kaydı satırları güncellenemez ve silinemez; ekleme serbesttir. Her işlemi ayrı bir tetikleyici reddeder.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class O04_DenetimKaydiKorumaTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task O04_DenetimKaydiSatiri_GuncellemeVeSilme_TetikleyiciyleReddedilir(bool sil)
    {
        int id;
        await using (var db = fixture.YeniContext())
        {
            var kayit = new AuditLog { Action = "Ilk", EntityType = "Deneme", Timestamp = new DateTime(2027, 3, 10, 12, 0, 0, DateTimeKind.Utc) };
            db.AuditLogs.Add(kayit);
            await db.SaveChangesAsync();
            id = kayit.Id;
        }

        await using (var db = fixture.YeniContext())
        {
            var hata = await Assert.ThrowsAsync<PostgresException>(() => DegistirAsync(db, id, sil));
            Assert.Equal("P0001", hata.SqlState);
            Assert.Equal($"AuditLog tablosunda güncelleme ve silme işlemleri yasaktır. Kayıt ID: {id}", hata.MessageText);
        }
        Assert.Equal(new[] { "Ilk" }, await EylemlerAsync());

        if (!sil)
        {
            // Güncelleme tetikleyicisi sütuna bağlı değildir: başka bir sütunun güncellenmesi de aynı hatayla reddedilir.
            await using var db = fixture.YeniContext();
            var hata = await Assert.ThrowsAsync<PostgresException>(() =>
                db.AuditLogs.Where(k => k.Id == id).ExecuteUpdateAsync(u => u.SetProperty(k => k.EntityType, "Baska")));
            Assert.Equal("P0001", hata.SqlState);
            Assert.Equal($"AuditLog tablosunda güncelleme ve silme işlemleri yasaktır. Kayıt ID: {id}", hata.MessageText);
        }

        // Kontrol: işlem içinde yalnız bu tetikleyici kapatılınca aynı komut satırı etkiler. İşlem her durumda geri alınır
        // (hata olursa işlemin kapatılması geri alır), tetikleyici kalıcı olarak kapanmaz.
        await using (var db = fixture.YeniContext())
        await using (var islem = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync(sil
                ? "ALTER TABLE \"AuditLogs\" DISABLE TRIGGER trg_audit_log_no_delete"
                : "ALTER TABLE \"AuditLogs\" DISABLE TRIGGER trg_audit_log_no_update");
            Assert.Equal(1, await DegistirAsync(db, id, sil));
            await islem.RollbackAsync();
        }
        Assert.Equal(new[] { "Ilk" }, await EylemlerAsync());
        Assert.Equal(new[] { "trg_audit_log_no_delete:O::11:true", "trg_audit_log_no_update:O::19:true" }, await TetikleyicilerAsync());
    }

    private static Task<int> DegistirAsync(AppDbContext db, int id, bool sil) => sil
        ? db.AuditLogs.Where(k => k.Id == id).ExecuteDeleteAsync()
        : db.AuditLogs.Where(k => k.Id == id).ExecuteUpdateAsync(u => u.SetProperty(k => k.Action, "Degisti"));

    // Denetim kaydı satırlarının eylemleri, yeni bir context'le.
    private async Task<string[]> EylemlerAsync()
    {
        await using var db = fixture.YeniContext();
        return await db.AuditLogs.Select(k => k.Action).ToArrayAsync();
    }

    // Denetim kaydı tablosundaki tetikleyiciler: etkinlik durumu (O: etkin), sütun listesi (boş: bütün sütunlar), tür bitleri
    // (satır başına + önce + silme = 11; satır başına + önce + güncelleme = 19) ve satır koşulunun yokluğu.
    private async Task<List<string>> TetikleyicilerAsync()
    {
        await using var baglanti = await fixture.BaglantiAcAsync();
        await using var komut = new NpgsqlCommand(
            "select tgname || ':' || tgenabled::text || ':' || tgattr::text || ':' || tgtype::text || ':' || (tgqual is null)::text from pg_trigger where tgrelid = '\"AuditLogs\"'::regclass and not tgisinternal order by 1",
            baglanti);
        await using var okuyucu = await komut.ExecuteReaderAsync();
        var sonuc = new List<string>();
        while (await okuyucu.ReadAsync())
        {
            sonuc.Add(okuyucu.GetString(0));
        }
        return sonuc;
    }
}
