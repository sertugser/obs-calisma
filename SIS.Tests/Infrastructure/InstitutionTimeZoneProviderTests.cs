using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;

namespace SIS.Tests.Infrastructure;

// Sağlayıcı gerçek SystemConfigProvider ve MemoryCache üzerinden okur; kendi önbelleği yoktur.
public sealed class InstitutionTimeZoneProviderTests
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly SystemConfigProvider _ayarlar;
    private readonly InstitutionTimeZoneProvider _saglayici;

    public InstitutionTimeZoneProviderTests()
    {
        _ayarlar = new SystemConfigProvider(_db, new MemoryCache(new MemoryCacheOptions()));
        _saglayici = new InstitutionTimeZoneProvider(_ayarlar);
    }

    private SystemConfig Yaz(string deger)
    {
        var ayar = _db.SystemConfigs.Add(new SystemConfig { Key = "Institution.TimeZone", Value = deger, DataType = "string" }).Entity;
        _db.SaveChanges();
        return ayar;
    }

    [Fact]
    public async Task AnahtarYoksa_Utc() => Assert.Equal("Etc/UTC", (await _saglayici.GetAsync()).Id);

    [Fact]
    public async Task KayitliDilim_Doner()
    {
        Yaz("Europe/Istanbul");
        Assert.Equal("Europe/Istanbul", (await _saglayici.GetAsync()).Id);
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("Turkey Standard Time")]
    [InlineData("")]
    public async Task GecersizDeger_AcikHataVerir_UtcyeDusmez(string deger)
    {
        Yaz(deger);
        var hata = await Assert.ThrowsAsync<InstitutionTimeZoneConfigurationException>(() => _saglayici.GetAsync());
        Assert.Contains("Sistem Ayarları", hata.Message);
    }

    [Fact]
    public async Task OnbellekTemizlenince_YeniDegerOkunur()
    {
        var ayar = Yaz("Europe/Istanbul");
        await _saglayici.GetAsync();
        ayar.Value = "Europe/Berlin";
        _db.SaveChanges();
        Assert.Equal("Europe/Istanbul", (await _saglayici.GetAsync()).Id); // önbellekte eski değer

        _ayarlar.InvalidateCache("Institution.TimeZone");

        Assert.Equal("Europe/Berlin", (await _saglayici.GetAsync()).Id);
    }
}
