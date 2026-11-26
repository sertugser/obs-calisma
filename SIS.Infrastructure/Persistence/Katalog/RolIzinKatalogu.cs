using Microsoft.EntityFrameworkCore;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence.Seeds;

namespace SIS.Infrastructure.Persistence.Katalog;

public sealed record RolIzinKaydi(string Rol, string Izin);

// Rollerin varsayılan izinleri (liste: RolePermissionSeed.RoleDefaults). Atamalar yöneticinin
// (PermissionService, PermissionBundleService): katalog yalnız eksik atamayı ekler, hiçbirini
// kaldırmaz; yöneticinin kaldırdığı atama geri gelmez. Katalogdan önce kurulmuş bir sistemde ilk
// eşitleme hiçbir şey eklemez, var olan durumu kayda alır. SystemAdministrator izinleri kod
// kısayoluyla aldığı için satırı yoktur.
public static class RolIzinKatalogu
{
    public static IReadOnlyList<RolIzinKaydi> Kayitlar { get; } =
        RolePermissionSeed.RoleDefaults
            .SelectMany(r => r.Value.Select(izin => new RolIzinKaydi(r.Key, izin)))
            .ToList();

    public static string Anahtar(string rol, string izin) => $"{rol}|{izin}";

    public static async Task<KatalogSonucu> EsitleAsync(AppDbContext db, DateTime simdi, CancellationToken ct = default)
    {
        var roller = await db.Roles.Where(r => r.Name != null).ToDictionaryAsync(r => r.Name!, r => r.Id, StringComparer.Ordinal, ct);
        var izinler = await db.Permissions.ToDictionaryAsync(p => p.Key, p => p.Id, StringComparer.Ordinal, ct);
        var rolAdi = roller.ToDictionary(r => r.Value, r => r.Key);
        var izinAnahtari = izinler.ToDictionary(i => i.Value, i => i.Key);

        // Rolü ya da izni veritabanında olmayan kayıt (yönetici silmiş olamaz; katalog hatası) kurulumu durdurur.
        var eksik = Kayitlar.Where(k => !roller.ContainsKey(k.Rol) || !izinler.ContainsKey(k.Izin)).ToList();
        if (eksik.Count > 0)
            throw new InvalidOperationException(
                $"Rol-izin kataloğunda veritabanında olmayan rol ya da izin: {string.Join(", ", eksik.Select(k => Anahtar(k.Rol, k.Izin)))}");

        return await KatalogEsitleyici.EsitleAsync(db, new KatalogTuru<RolePermission, RolIzinKaydi>
        {
            Ad = "rol-izin",
            Kayitlar = Kayitlar,
            KayitAnahtari = k => Anahtar(k.Rol, k.Izin),
            // Kaynağı ne olursa olsun (elle ya da paketten) aynı rol-izin çifti tek satırdır (tekil dizin).
            VarlikAnahtari = rp => Anahtar(rolAdi.GetValueOrDefault(rp.RoleId, $"#{rp.RoleId}"), izinAnahtari.GetValueOrDefault(rp.PermissionId, $"#{rp.PermissionId}")),
            Olustur = (k, an) => new RolePermission
            {
                RoleId = roller[k.Rol],
                PermissionId = izinler[k.Izin],
                GrantedAt = an,
                GrantedByUserId = null,
            },
            DoluTablodaIlkEsitlemedeEkleme = true,
        }, simdi, ct);
    }
}
