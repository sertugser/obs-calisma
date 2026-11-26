using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Postgres;

// A-01: boş (yalnız migration uygulanmış) veritabanında API açılışı, seed, admin girişi ve menü.
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class KurulumVeGirisTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Development_SeedAdminGirisiVeMenu()
    {
        await using var uygulama = new SisUygulamasi("Development", fixture.BaglantiCumlesi);
        using var istemci = uygulama.CreateClient(); // Development'ta seed açılışta çalışır.

        // Seed'deki sabit parola teste yazılmaz: admin parolası çalışma anında rastgele değere çevrilir.
        string email;
        var password = RastgeleParola();
        using (var kapsam = uygulama.Services.CreateScope())
        {
            var kullanicilar = kapsam.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = Assert.Single(await kullanicilar.GetUsersInRoleAsync("SystemAdministrator"));
            email = admin.Email!;
            var jeton = await kullanicilar.GeneratePasswordResetTokenAsync(admin);
            Assert.True((await kullanicilar.ResetPasswordAsync(admin, jeton, password)).Succeeded);
        }

        var giris = await istemci.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, giris.StatusCode);
        using var girisGovde = JsonDocument.Parse(await giris.Content.ReadAsStringAsync());
        Assert.True(girisGovde.RootElement.GetProperty("success").GetBoolean());
        var token = girisGovde.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrEmpty(token));

        istemci.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var menu = await istemci.GetAsync("/api/me/menu");
        Assert.Equal(HttpStatusCode.OK, menu.StatusCode);
        using var menuGovde = JsonDocument.Parse(await menu.Content.ReadAsStringAsync());
        Assert.True(menuGovde.RootElement.GetArrayLength() > 0);

        // Demo tohumu kurum saat dilimini örnek kurumunkine çeker.
        await using var db = fixture.YeniContext();
        Assert.Equal("Europe/Istanbul", await db.SystemConfigs.Where(c => c.Key == "Institution.TimeZone").Select(c => c.Value).SingleAsync());
    }

    [Fact]
    public async Task Production_SeedCalismaz_GirisBasarisiz()
    {
        await using var uygulama = new SisUygulamasi("Production", fixture.BaglantiCumlesi);
        using var istemci = uygulama.CreateClient();

        await using (var db = fixture.YeniContext())
        {
            Assert.Equal(0, await db.Users.CountAsync());
        }

        var giris = await istemci.PostAsJsonAsync("/api/auth/login", new { email = "yok@ornek.test", password = RastgeleParola() });
        Assert.Equal(HttpStatusCode.Unauthorized, giris.StatusCode);
    }

    // Büyük harf, küçük harf, rakam ve simge içeren rastgele parola.
    private static string RastgeleParola() => "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    // Program.cs olduğu gibi çalışır; yapılandırma yalnız test değerleriyle ezilir.
    private sealed class SisUygulamasi(string ortam, string baglantiCumlesi) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(ortam);
            builder.UseSetting("ConnectionStrings:DefaultConnection", baglantiCumlesi);
            builder.UseSetting("Jwt:Key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("Jwt:Issuer", "sis-test");
            builder.UseSetting("Jwt:Audience", "sis-test");
            builder.UseSetting("Jwt:ExpiresInMinutes", "30");
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
            builder.UseSetting("AllowedHosts", "localhost");
        }
    }
}
