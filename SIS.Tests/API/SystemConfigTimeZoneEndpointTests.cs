using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.API.Controllers;
using SIS.Application.Common.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.API;

// Kurum saat dilimi ayarı PUT ve içe aktarımda aynı doğrulayıcıdan geçer.
public sealed class SystemConfigTimeZoneEndpointTests
{
    private const string Anahtar = "Institution.TimeZone";
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly SystemConfigController _denetleyici;
    private readonly int _id;

    public SystemConfigTimeZoneEndpointTests()
    {
        var ayar = _db.SystemConfigs.Add(new SystemConfig { Key = Anahtar, Value = "Etc/UTC", DataType = "string" }).Entity;
        _db.SaveChanges();
        _id = ayar.Id;
        _denetleyici = new SystemConfigController(_db, new NullSystemConfigProvider());
    }

    private string Kayitli() => _db.SystemConfigs.AsNoTracking().Single().Value;

    private Task<IActionResult> Put(string deger) => _denetleyici.Update(_id, new UpdateSystemConfigDto(deger), default);

    private Task<IActionResult> Ice(string deger) => _denetleyici.Import([new SystemConfigImportItemDto(Anahtar, deger)], default);

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("Turkey Standard Time")]
    [InlineData(" ")]
    [InlineData("Europe//Istanbul")]
    public async Task GecersizAd_400_DegerDegismez(string deger)
    {
        Assert.IsType<BadRequestObjectResult>(await Put(deger));
        Assert.IsType<BadRequestObjectResult>(await Ice(deger));
        Assert.Equal("Etc/UTC", Kayitli());
    }

    [Fact]
    public async Task GecerliAd_KaydedilirPutVeIceAktarim()
    {
        Assert.IsType<OkObjectResult>(await Put("Europe/Istanbul"));
        Assert.Equal("Europe/Istanbul", Kayitli());

        Assert.IsType<OkObjectResult>(await Ice("Europe/Berlin"));
        Assert.Equal("Europe/Berlin", Kayitli());
    }

    // Büyük-küçük harf ve baştaki/sondaki boşluk normal biçime çevrilir. antarctica/troll'ü başka hiçbir test
    // adıyla yüklemez; böylece sonuç .NET'in süreç içi saat dilimi önbelleğine bağlı kalmaz.
    [Theory]
    [InlineData("europe/istanbul", "Europe/Istanbul")]
    [InlineData("  Europe/Istanbul  ", "Europe/Istanbul")]
    [InlineData("antarctica/troll", "Antarctica/Troll")]
    public async Task AdNormalBicimdeSaklanir(string deger, string beklenen)
    {
        Assert.IsType<OkObjectResult>(await Put(deger));
        Assert.Equal(beklenen, Kayitli());
        Assert.IsType<OkObjectResult>(await Ice("Etc/UTC"));
        Assert.IsType<OkObjectResult>(await Ice(deger));
        Assert.Equal(beklenen, Kayitli());
    }
}
