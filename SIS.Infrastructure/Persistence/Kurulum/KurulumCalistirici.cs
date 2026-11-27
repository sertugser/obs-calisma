using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SIS.Application.Common.Time;

namespace SIS.Infrastructure.Persistence.Kurulum;

// `SIS.API setup [--seed-demo]` komutunun seçenekleri.
public sealed record KurulumSecenekleri(bool DemoVerisi, string? YoneticiEposta, string? YoneticiParola, string? Kurum = null)
{
    public const string Komut = "setup";
    public const string DemoBayragi = "--seed-demo";
    public const string KurumBayragi = "--kurum";
    public const string EpostaDegiskeni = "SIS_SETUP_ADMIN_EMAIL";
    public const string ParolaDegiskeni = "SIS_SETUP_ADMIN_PASSWORD";

    public static bool KurulumKomutuMu(string[] args) => args.Length > 0 && args[0] == Komut;

    public static KurulumSecenekleri Oku(string[] args, Func<string, string?> ortamDegiskeni)
    {
        var kurumYeri = Array.IndexOf(args, KurumBayragi);
        string? kurum = null;
        if (kurumYeri > 0)
        {
            if (kurumYeri + 1 >= args.Length || args[kurumYeri + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"{KurumBayragi} bir kurum adı ister, ör. '{KurumBayragi} yerel'.");
            kurum = args[kurumYeri + 1];
        }
        return new(args.Skip(1).Contains(DemoBayragi),
            ortamDegiskeni(EpostaDegiskeni),
            ortamDegiskeni(ParolaDegiskeni),
            kurum);
    }
}

// Boş ya da var olan bir veritabanını kullanıma hazırlar: migration, sistem verisi, ilk yönetici,
// istenirse örnek veri. Tekrar çalıştırılması güvenlidir. Başarıda 0, hatada 1 döner.
public static class KurulumCalistirici
{
    public const string YoneticiRolu = "SystemAdministrator";

    public static async Task<int> CalistirAsync(
        IServiceProvider servisler, KurulumSecenekleri secenekler, ILogger gunluk, CancellationToken ct = default)
    {
        var adim = "seçenekler";
        try
        {
            // Kurum adı veritabanına dokunmadan önce doğrulanır.
            var kurumKaynagi = secenekler.Kurum is null ? null : Kurum.KurumVeriKaynaklari.Bul(secenekler.Kurum);

            adim = "migration";
            await using (var kapsam = servisler.CreateAsyncScope())
            {
                var db = kapsam.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync(ct);
            }
            gunluk.LogInformation("Kurulum: migration'lar uygulandı.");

            adim = "sistem verisi";
            var saatDilimiYeni = false;
            await AdimdaCalistirAsync(servisler, async sp =>
                saatDilimiYeni = await SeedData.SeedSystemAsync(
                    sp.GetRequiredService<AppDbContext>(),
                    sp.GetRequiredService<RoleManager<IdentityRole<int>>>()), ct);
            gunluk.LogInformation("Kurulum: sistem verisi yazıldı.");

            adim = "ilk yönetici";
            var olustu = false;
            await AdimdaCalistirAsync(servisler, async sp =>
                olustu = await IlkYoneticiyiOlusturAsync(sp, secenekler), ct);
            gunluk.LogInformation(olustu
                ? "Kurulum: ilk yönetici oluşturuldu."
                : "Kurulum: yönetici zaten var, dokunulmadı.");

            if (kurumKaynagi is not null)
            {
                adim = "kurum verisi";
                await AdimdaCalistirAsync(servisler, async sp =>
                    await kurumKaynagi.YazAsync(sp.GetRequiredService<AppDbContext>(), ct), ct);
                gunluk.LogInformation("Kurulum: '{Kurum}' kurum verisi yazıldı.", kurumKaynagi.Ad);
            }

            if (secenekler.DemoVerisi)
            {
                adim = "örnek veri";
                await AdimdaCalistirAsync(servisler, async sp =>
                    await SeedData.SeedDemoAsync(
                        sp.GetRequiredService<AppDbContext>(),
                        sp.GetRequiredService<UserManager<ApplicationUser>>(), saatDilimiYeni), ct);
                gunluk.LogInformation("Kurulum: örnek veri yazıldı.");
            }

            gunluk.LogInformation("Kurulum tamamlandı.");
            return 0;
        }
        catch (Exception ex)
        {
            // İleti parola içermez: Identity hataları yalnız kural adlarını verir.
            gunluk.LogError("Kurulum '{Adim}' adımında durdu: {Ileti} ({Neden})",
                adim, ex.Message, ex.GetBaseException().Message);
            return 1;
        }
    }

    // Adımı kendi kapsamında ve tek transaction'da çalıştırır; hata olursa adımın yazdıkları geri alınır.
    private static async Task AdimdaCalistirAsync(
        IServiceProvider servisler, Func<IServiceProvider, Task> calistir, CancellationToken ct)
    {
        await using var kapsam = servisler.CreateAsyncScope();
        var db = kapsam.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var islem = await db.Database.BeginTransactionAsync(ct);
        await calistir(kapsam.ServiceProvider);
        await islem.CommitAsync(ct);
    }

    private static async Task<bool> IlkYoneticiyiOlusturAsync(IServiceProvider sp, KurulumSecenekleri secenekler)
    {
        var kullanicilar = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if ((await kullanicilar.GetUsersInRoleAsync(YoneticiRolu)).Count > 0)
            return false;

        if (string.IsNullOrWhiteSpace(secenekler.YoneticiEposta))
            throw new InvalidOperationException(
                $"Yönetici yok ve {KurulumSecenekleri.EpostaDegiskeni} ortam değişkeni boş.");
        if (string.IsNullOrEmpty(secenekler.YoneticiParola))
            throw new InvalidOperationException(
                $"Yönetici yok ve {KurulumSecenekleri.ParolaDegiskeni} ortam değişkeni boş.");

        var eposta = secenekler.YoneticiEposta.Trim();
        var yonetici = new ApplicationUser
        {
            UserName = eposta,
            Email = eposta,
            FirstName = "Sistem",
            LastName = "Yöneticisi",
            EmailConfirmed = true,
            PasswordChangedAtUtc = sp.GetRequiredService<IDateTimeProvider>().UtcNow,
        };
        Basarili(await kullanicilar.CreateAsync(yonetici, secenekler.YoneticiParola), "Yönetici oluşturulamadı");
        Basarili(await kullanicilar.AddToRoleAsync(yonetici, YoneticiRolu), "Yöneticiye rol verilemedi");
        return true;
    }

    private static void Basarili(IdentityResult sonuc, string ileti)
    {
        if (!sonuc.Succeeded)
            throw new InvalidOperationException(
                $"{ileti}: {string.Join(", ", sonuc.Errors.Select(e => e.Code))}");
    }
}
