using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIS.Domain.Enums;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Postgres.Yetki;

/// <summary>
/// Yetki testleri için rol, izin ve kullanıcı kurucu: roller ve izin satırları testte kurulur (seed yok).
/// Parola çalışma anında rastgele üretilir ve yalnız gerçek giriş isteğinde kullanılır.
/// </summary>
internal sealed class YetkiKurucu(PostgresFixture fixture, WebApplicationFactory<Program> uygulama)
{
    private readonly Dictionary<string, string> _parolalar = [];

    /// <summary>Rolü (yoksa) oluşturur, verilen izin anahtarlarını role bağlar; rolün kimliğini döndürür.</summary>
    public async Task<int> RolAsync(string rol, params string[] izinler)
    {
        using (var kapsam = uygulama.Services.CreateScope())
        {
            var roller = kapsam.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            if (!await roller.RoleExistsAsync(rol))
                Assert.True((await roller.CreateAsync(new IdentityRole<int>(rol))).Succeeded);
        }

        await using var db = fixture.YeniContext();
        var rolId = await db.Roles.Where(r => r.Name == rol).Select(r => r.Id).SingleAsync();
        foreach (var anahtar in izinler)
        {
            var izin = await db.Permissions.SingleOrDefaultAsync(p => p.Key == anahtar)
                ?? db.Permissions.Add(new Permission { Key = anahtar, Category = PermissionCategory.Menu }).Entity;
            db.RolePermissions.Add(new RolePermission { RoleId = rolId, Permission = izin });
        }

        await db.SaveChangesAsync();
        return rolId;
    }

    /// <summary>Kullanıcıyı oluşturur (rol verilirse role ekler); kullanıcının kimliğini döndürür.</summary>
    public async Task<int> KullaniciAsync(string email, string? rol = null)
    {
        var password = RastgeleParola();
        using var kapsam = uygulama.Services.CreateScope();
        var kullanicilar = kapsam.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var kullanici = new ApplicationUser { UserName = email, Email = email, FirstName = "Deneme", LastName = "Kullanıcı" };
        Assert.True((await kullanicilar.CreateAsync(kullanici, password)).Succeeded);
        if (rol != null)
            Assert.True((await kullanicilar.AddToRoleAsync(kullanici, rol)).Succeeded);
        _parolalar[email] = password;
        return kullanici.Id;
    }

    /// <summary>Gerçek giriş ucu; doğru parola yerine istenirse rastgele (yanlış) bir parola gönderilir.</summary>
    public Task<HttpResponseMessage> GirisAsync(HttpClient istemci, string email, bool dogruParola = true)
    {
        var password = dogruParola ? _parolalar[email] : RastgeleParola();
        return istemci.PostAsJsonAsync("/api/auth/login", new { email, password });
    }

    /// <summary>Gerçek girişle alınan jetonu taşıyan istemci.</summary>
    public async Task<HttpClient> IstemciAsync(string email)
    {
        var istemci = uygulama.CreateClient();
        var giris = await GirisAsync(istemci, email);
        Assert.Equal(HttpStatusCode.OK, giris.StatusCode);
        using var govde = JsonDocument.Parse(await giris.Content.ReadAsStringAsync());
        istemci.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", govde.RootElement.GetProperty("token").GetString());
        return istemci;
    }

    // Büyük harf, küçük harf, rakam ve simge içeren rastgele parola.
    private static string RastgeleParola() => "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
