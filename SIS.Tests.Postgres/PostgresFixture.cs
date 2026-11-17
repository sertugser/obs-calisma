using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Respawn.Graph;
using SIS.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SIS.Tests.Postgres;

// Bütün gerçek PostgreSQL testleri bu koleksiyonda: tek konteyner, tek migration, sıralı çalışma.
[CollectionDefinition(Ad)]
public sealed class PostgresKoleksiyonu : ICollectionFixture<PostgresFixture>
{
    public const string Ad = "Postgres";
}

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Imaj = "postgres:18.7-trixie";
    private const string Gecmis = "__EFMigrationsHistory";

    private readonly PostgreSqlContainer _konteyner = new PostgreSqlBuilder(Imaj).Build();
    private Respawner? _respawner;
    private string _geriYukle = string.Empty;

    public string BaglantiCumlesi => _konteyner.GetConnectionString();

    // Migration'ın veri yazdığı tablolar ve migration sonrası satır sayıları.
    public IReadOnlyDictionary<string, long> MigrationVerisi { get; private set; } = new Dictionary<string, long>();

    public async Task InitializeAsync()
    {
        await _konteyner.StartAsync();
        await using (var db = YeniContext())
        {
            await db.Database.MigrateAsync();
        }

        await using var baglanti = await BaglantiAcAsync();
        MigrationVerisi = (await SatirSayilariAsync(baglanti)).Where(t => t.Value > 0).ToDictionary();

        // TRUNCATE ... CASCADE yabancı anahtarla bağlı tabloları da boşalttığı için hariç tutmak yetmez:
        // migration verisi sablon şemasına kopyalanır, her temizlikten sonra geri yüklenir.
        var kopyalanacak = MigrationVerisi.Keys.Where(t => t != Gecmis).ToList();
        await CalistirAsync(baglanti, "create schema sablon;" + string.Concat(
            kopyalanacak.Select(t => $"create table sablon.\"{t}\" as table public.\"{t}\";")));
        _geriYukle = "set session_replication_role = replica;" + string.Concat(
            kopyalanacak.Select(t => $"insert into public.\"{t}\" overriding system value select * from sablon.\"{t}\";"))
            + "set session_replication_role = default;";

        _respawner = await Respawner.CreateAsync(baglanti, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("public", Gecmis)],
        });
    }

    public Task DisposeAsync() => _konteyner.DisposeAsync().AsTask();

    public AppDbContext YeniContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(BaglantiCumlesi).Options);

    public async Task<NpgsqlConnection> BaglantiAcAsync()
    {
        var baglanti = new NpgsqlConnection(BaglantiCumlesi);
        await baglanti.OpenAsync();
        return baglanti;
    }

    // Her testten önce çağrılır: veritabanını migration sonrası hâline döndürür.
    public async Task TemizleAsync()
    {
        await using var baglanti = await BaglantiAcAsync();
        await _respawner!.ResetAsync(baglanti);
        await CalistirAsync(baglanti, _geriYukle);
    }

    // public şemasındaki her tablonun satır sayısı.
    public static async Task<Dictionary<string, long>> SatirSayilariAsync(NpgsqlConnection baglanti)
    {
        var tablolar = new List<string>();
        await using (var komut = new NpgsqlCommand(
            "select table_name from information_schema.tables where table_schema = 'public' and table_type = 'BASE TABLE'",
            baglanti))
        await using (var okuyucu = await komut.ExecuteReaderAsync())
        {
            while (await okuyucu.ReadAsync())
            {
                tablolar.Add(okuyucu.GetString(0));
            }
        }

        var sayilar = new Dictionary<string, long>();
        foreach (var tablo in tablolar)
        {
            await using var komut = new NpgsqlCommand($"select count(*) from public.\"{tablo}\"", baglanti);
            sayilar[tablo] = (long)(await komut.ExecuteScalarAsync())!;
        }

        return sayilar;
    }

    private static async Task CalistirAsync(NpgsqlConnection baglanti, string sql)
    {
        await using var komut = new NpgsqlCommand(sql, baglanti);
        await komut.ExecuteNonQueryAsync();
    }
}
