using SIS.Application.Common.Time;

namespace SIS.Tests.Application;

public class InstitutionTimeZoneValidatorTests
{
    [Theory]
    [InlineData("Europe/Istanbul")]
    [InlineData("Etc/UTC")]
    public void IanaAdi_Gecerlidir(string ad)
    {
        Assert.True(InstitutionTimeZoneValidator.TryResolve(ad, out var tz, out var hata));
        Assert.Equal((ad, ""), (tz.Id, hata));
    }

    [Theory]
    [InlineData(null, "boş")]
    [InlineData("", "boş")]
    [InlineData("   ", "boş")]
    [InlineData("Mars/Olympus", "bulunamadı")]
    public void GecersizAd_NedeniyleReddedilir(string? ad, string neden)
    {
        Assert.False(InstitutionTimeZoneValidator.TryResolve(ad, out _, out var hata));
        Assert.Contains(neden, hata);
    }

    // Windows adı her ortamda reddedilir; nedeni (bulunamadı ya da IANA değil) platformun saat dilimi verisine bağlıdır.
    [Fact]
    public void WindowsAdi_Reddedilir() =>
        Assert.False(InstitutionTimeZoneValidator.TryResolve("Turkey Standard Time", out _, out _));

    // UTC işletim sisteminin saat dilimi verisine bakılmadan çözülür; saklanan biçim Etc/UTC'dir.
    [Theory]
    [InlineData("UTC")]
    [InlineData("utc")]
    [InlineData("Etc/UTC")]
    [InlineData("etc/utc")]
    public void Utc_HerOrtamdaKabulEdilir(string ad)
    {
        Assert.True(InstitutionTimeZoneValidator.TryResolve(ad, out var tz, out _));
        Assert.Equal(("Etc/UTC", TimeSpan.Zero), (tz.Id, tz.BaseUtcOffset));
        Assert.False(tz.SupportsDaylightSavingTime);
    }

    // Sunucunun kendi dilimine ya da tzdata iç dosyalarına işaret eden adlar, sunucuda bulunsa da bulunmasa da reddedilir.
    [Theory]
    [InlineData("localtime")]
    [InlineData("posixrules")]
    [InlineData("Factory")]
    [InlineData("right/Europe/Istanbul")]
    [InlineData("posix/Europe/Istanbul")]
    public void SunucuyaBagliAd_Reddedilir(string ad)
    {
        Assert.False(InstitutionTimeZoneValidator.TryResolve(ad, out _, out var hata));
        Assert.Contains("kullanılamaz", hata);
    }
}
