using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Postgres.DersProgrami;

/// <summary>
/// Test sınıfı başına bir kez açılan uygulama: Program.cs olduğu gibi çalışır (Production, seed yok);
/// yapılandırma yalnız test değerleriyle ezilir.
/// </summary>
public sealed class DersProgramiUygulamasi : IDisposable
{
    private Fabrika? _fabrika;

    public WebApplicationFactory<Program> Al(string baglantiCumlesi) => _fabrika ??= new Fabrika(baglantiCumlesi);

    public void Dispose() => _fabrika?.Dispose();

    private sealed class Fabrika(string baglantiCumlesi) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
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

/// <summary>
/// Ders programı testleri için gerçek PostgreSQL veri kurucu: bir fakültenin bölümünde tek bir ders (1 saat teori),
/// bir dönemde Pazartesi iki ardışık zaman dilimi (Z1 09:00-09:50, Z2 10:00-10:50), iki derslik ve bir hoca.
/// Fakülte ve dönem kimlikleri her koşuda birbirinden farklıdır. Testler belirli bir Id'ye dayanmaz.
/// </summary>
internal sealed class DersProgramiVeriKurucu(PostgresFixture fixture)
{
    public const string DersKodu = "DPR101";
    public static readonly (TimeOnly Bas, TimeOnly Bit) Z1 = (new(9, 0), new(9, 50));
    public static readonly (TimeOnly Bas, TimeOnly Bit) Z2 = (new(10, 0), new(10, 50));

    public int FakulteId { get; private set; }
    public int DonemId { get; private set; }
    public int DersId { get; private set; }
    public int Z1Id { get; private set; }
    public int Z2Id { get; private set; }
    public int D1Id { get; private set; }
    public int D2Id { get; private set; }
    public string D1Adi => "DP-101";
    public int HocaId { get; private set; }
    public int KullaniciId { get; private set; }

    public static async Task<DersProgramiVeriKurucu> KurAsync(PostgresFixture fixture)
    {
        var veri = new DersProgramiVeriKurucu(fixture);
        var bolum = await veri.KaydetAsync(new Department
        {
            Name = "Deneme Bölümü", Code = "DPB", Faculty = new Faculty { Name = "Deneme Fakültesi", Code = "DPF" }
        });
        veri.FakulteId = bolum.FacultyId;
        // İki aday dönemden kimliği fakülteninkine denk gelmeyeni kullanılır.
        var donemler = new[] { await veri.DonemEkleAsync(2031), await veri.DonemEkleAsync(2032) };
        veri.DonemId = donemler.First(d => d != veri.FakulteId);
        veri.DersId = (await veri.KaydetAsync(new Course
        {
            Code = DersKodu, Name = "Program Deneme Dersi", Ects = 5, NationalCredit = 3, TheoryHours = 1, DepartmentId = bolum.Id
        })).Id;
        veri.Z1Id = (await veri.KaydetAsync(Dilim(veri.DonemId, Z1, 1))).Id;
        veri.Z2Id = (await veri.KaydetAsync(Dilim(veri.DonemId, Z2, 2))).Id;
        veri.D1Id = (await veri.KaydetAsync(Derslik(veri.D1Adi))).Id;
        veri.D2Id = (await veri.KaydetAsync(Derslik("DP-102"))).Id;
        var kullanici = await veri.KaydetAsync(new ApplicationUser { UserName = "dphoca", FirstName = "Deneme", LastName = "Hoca" });
        veri.KullaniciId = kullanici.Id;
        veri.HocaId = (await veri.KaydetAsync(new Instructor
        {
            UserId = kullanici.Id, DepartmentId = bolum.Id, Title = "Dr.", MaxWeeklyHours = 20
        })).Id;
        return veri;
    }

    public async Task<int> SubeEkleAsync(int no) => (await KaydetAsync(new CourseSection
    {
        CourseId = DersId, TermId = DonemId, SectionNumber = no, Capacity = 30, Status = CourseSectionStatus.Active
    })).Id;

    /// <summary>Şubenin program satırları (derslik, zaman dilimi), yeni bir context'le.</summary>
    public async Task<(int?, int)[]> ProgramAsync(int subeId)
    {
        await using var db = fixture.YeniContext();
        var satirlar = await db.SectionSchedules.Where(s => s.CourseSectionId == subeId)
            .OrderBy(s => s.TimeSlotId).Select(s => new { s.ClassroomId, s.TimeSlotId }).ToListAsync();
        return satirlar.Select(s => (s.ClassroomId, s.TimeSlotId)).ToArray();
    }

    /// <summary>SystemAdministrator rollü kullanıcı; parola çalışma anında rastgele üretilir. Gerçek girişle jeton alınır.</summary>
    public static async Task<HttpClient> YoneticiIstemcisiAsync(WebApplicationFactory<Program> uygulama)
    {
        const string rol = "SystemAdministrator";
        var password = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var email = "dp-yonetici@ornek.test";
        using (var kapsam = uygulama.Services.CreateScope())
        {
            var roller = kapsam.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            if (!await roller.RoleExistsAsync(rol))
                Assert.True((await roller.CreateAsync(new IdentityRole<int>(rol))).Succeeded);
            var kullanicilar = kapsam.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var yonetici = new ApplicationUser { UserName = email, Email = email, FirstName = "Deneme", LastName = "Yönetici" };
            Assert.True((await kullanicilar.CreateAsync(yonetici, password)).Succeeded);
            Assert.True((await kullanicilar.AddToRoleAsync(yonetici, rol)).Succeeded);
        }

        var istemci = uygulama.CreateClient();
        var giris = await istemci.PostAsJsonAsync("/api/auth/login", new { email, password });
        giris.EnsureSuccessStatusCode();
        using var govde = JsonDocument.Parse(await giris.Content.ReadAsStringAsync());
        istemci.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", govde.RootElement.GetProperty("token").GetString());
        return istemci;
    }

    private async Task<int> DonemEkleAsync(int yil) => (await KaydetAsync(new AcademicTerm
    {
        Name = $"{yil}-{yil + 1} Güz", ShortEnglishName = $"Fall {yil}", TermType = TermType.Fall, Year = yil,
        TermStart = new DateTime(yil, 9, 15, 0, 0, 0, DateTimeKind.Utc), TermEnd = new DateTime(yil + 1, 1, 15, 0, 0, 0, DateTimeKind.Utc)
    })).Id;

    private static TimeSlot Dilim(int donemId, (TimeOnly Bas, TimeOnly Bit) z, int sira) => new()
    {
        TermId = donemId, DayOfWeek = DayOfWeek.Monday, StartTime = z.Bas, EndTime = z.Bit, SlotOrder = sira, DisplayName = $"Pazartesi {sira}"
    };

    private static Classroom Derslik(string ad) => new() { Name = ad, BuildingName = "DP Blok", Capacity = 40, Type = ClassroomType.Lecture };

    private async Task<T> KaydetAsync<T>(T varlik) where T : class
    {
        await using var db = fixture.YeniContext();
        db.Add(varlik);
        await db.SaveChangesAsync();
        return varlik;
    }
}
