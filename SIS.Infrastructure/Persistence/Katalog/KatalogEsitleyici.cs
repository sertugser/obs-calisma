using Microsoft.EntityFrameworkCore;

namespace SIS.Infrastructure.Persistence.Katalog;

// Kataloğun bir kez yazdığı anahtar. Yönetici kaydı silse bile katalog onu yeniden eklemez.
public class KatalogKaydi
{
    public int Id { get; set; }
    public string Tur { get; set; } = string.Empty;
    public string Anahtar { get; set; } = string.Empty;
    public DateTime IlkYazilma { get; set; }
}

// Bir kez çalışan, adı olan katalog adımı (silme, yeniden adlandırma). Henüz adım yok.
public class KatalogAdimi
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public DateTime Calisti { get; set; }
}

// Bir katalog türü: kodda tanımlı kayıtlar ve veritabanındaki karşılıkları.
public sealed class KatalogTuru<TVarlik, TKayit> where TVarlik : class
{
    public required string Ad { get; init; }
    public required IReadOnlyList<TKayit> Kayitlar { get; init; }
    public required Func<TKayit, string> KayitAnahtari { get; init; }
    public required Func<TVarlik, string> VarlikAnahtari { get; init; }
    public required Func<TKayit, DateTime, TVarlik> Olustur { get; init; }

    // Var olan varlıkta yalnız kataloğun alanlarını yazar; bir şey değiştiyse true döner.
    public Func<TVarlik, TKayit, bool> KatalogAlanlariniYaz { get; init; } = (_, _) => false;

    // Tür daha önce hiç eşitlenmemişken tabloda satır varsa (katalogdan önce kurulmuş sistem) hiçbir şey
    // eklenmez; bütün anahtarlar kayda alınır. Yöneticinin kaldırdığı ile hiç verilmemiş olan ayırt
    // edilemediği için güvenli yön budur (eski seed de dolu tabloya dokunmuyordu).
    public bool DoluTablodaIlkEsitlemedeEkleme { get; init; }
}

public sealed record KatalogSonucu(string Tur, int Eklenen, int Guncellenen, int Kaydedilen, int Atlanan);

public static class KatalogEsitleyici
{
    // Eşitleme kuralı: veritabanında da kayıtta da olmayan anahtarı ekler; veritabanında olup kayıtta
    // olmayanı yalnız kayda alır; var olanda yalnız kataloğun alanlarını günceller; kayıtta olup
    // veritabanında olmayanı (yönetici silmiş) atlar. Hiçbir şey silmez.
    public static async Task<KatalogSonucu> EsitleAsync<TVarlik, TKayit>(
        AppDbContext db, KatalogTuru<TVarlik, TKayit> tur, DateTime simdi, CancellationToken ct = default)
        where TVarlik : class
    {
        AnahtarlariDenetle(tur.Ad, tur.Kayitlar.Select(tur.KayitAnahtari));

        var varliklar = (await db.Set<TVarlik>().ToListAsync(ct))
            .ToDictionary(tur.VarlikAnahtari, StringComparer.Ordinal);
        var kayitli = (await db.KatalogKayitlari.Where(k => k.Tur == tur.Ad).Select(k => k.Anahtar).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        int eklenen = 0, guncellenen = 0, kaydedilen = 0, atlanan = 0;
        if (tur.DoluTablodaIlkEsitlemedeEkleme && kayitli.Count == 0 && varliklar.Count > 0)
        {
            foreach (var kayit in tur.Kayitlar)
            {
                var anahtar = tur.KayitAnahtari(kayit);
                if (varliklar.TryGetValue(anahtar, out var varlik) && tur.KatalogAlanlariniYaz(varlik, kayit)) guncellenen++;
                db.KatalogKayitlari.Add(new KatalogKaydi { Tur = tur.Ad, Anahtar = anahtar, IlkYazilma = simdi });
                kaydedilen++;
            }
            await db.SaveChangesAsync(ct);
            return new KatalogSonucu(tur.Ad, 0, guncellenen, kaydedilen, 0);
        }

        foreach (var kayit in tur.Kayitlar)
        {
            var anahtar = tur.KayitAnahtari(kayit);
            var kayittaVar = kayitli.Contains(anahtar);
            if (varliklar.TryGetValue(anahtar, out var varlik))
            {
                if (tur.KatalogAlanlariniYaz(varlik, kayit)) guncellenen++;
                if (!kayittaVar)
                {
                    db.KatalogKayitlari.Add(new KatalogKaydi { Tur = tur.Ad, Anahtar = anahtar, IlkYazilma = simdi });
                    kaydedilen++;
                }
            }
            else if (kayittaVar)
            {
                atlanan++;
            }
            else
            {
                db.Set<TVarlik>().Add(tur.Olustur(kayit, simdi));
                db.KatalogKayitlari.Add(new KatalogKaydi { Tur = tur.Ad, Anahtar = anahtar, IlkYazilma = simdi });
                eklenen++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new KatalogSonucu(tur.Ad, eklenen, guncellenen, kaydedilen, atlanan);
    }

    public static void AnahtarlariDenetle(string tur, IEnumerable<string> anahtarlar)
    {
        var tekrar = anahtarlar.GroupBy(a => a, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (tekrar.Count > 0)
            throw new InvalidOperationException($"'{tur}' kataloğunda tekrar eden anahtar: {string.Join(", ", tekrar)}");
    }
}
