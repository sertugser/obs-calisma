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

    // Linux konteynerinde (tzdata 2026e) "Turkey Standard Time" bulunur ama HasIanaId false: IANA olmadığı için ret.
    [Theory]
    [InlineData(null, "boş")]
    [InlineData("", "boş")]
    [InlineData("   ", "boş")]
    [InlineData("Mars/Olympus", "bulunamadı")]
    [InlineData("Turkey Standard Time", "IANA")]
    public void GecersizAd_NedeniyleReddedilir(string? ad, string neden)
    {
        Assert.False(InstitutionTimeZoneValidator.TryResolve(ad, out _, out var hata));
        Assert.Contains(neden, hata);
    }

    [Fact]
    public void WindowsAdi_KonteynerdeBulunur_AmaIanaDegildir()
    {
        Assert.True(TimeZoneInfo.TryFindSystemTimeZoneById("Turkey Standard Time", out var bulunan));
        Assert.False(bulunan!.HasIanaId);
    }
}
