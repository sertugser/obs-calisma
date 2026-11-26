using System.Globalization;
using SIS.Domain.Entities;

namespace SIS.Tests.Domain;

// Çekilme son günü (D) kurumun yerel günü bitene kadar açık: bitiş = (D+1 yerel 00:00) − o andan hemen önceki ofset.
public class CekilmeSonGunuSaatDilimiTests
{
    private static AcademicTerm Donem(bool? zorla = null, string sonGun = "2025-05-10") => new()
    {
        IsActivated = true, TermStart = Utc("2025-02-15"), TermEnd = Utc("2025-12-31"),
        AcademicCalendar = new TermAcademicCalendar { WithdrawalDeadline = Utc(sonGun), WithdrawalOverride = zorla }
    };

    private static DateTime Utc(string s) =>
        DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    [Theory]
    // Europe/Istanbul, sabit +03:00: 11 Mayıs 00:00 yerel − 3 sa = 10 Mayıs 21:00Z.
    [InlineData("Europe/Istanbul", "2025-05-10", "2025-05-10T20:59:59Z", true)]
    [InlineData("Europe/Istanbul", "2025-05-10", "2025-05-10T21:00:00Z", false)]
    // Etc/UTC: 11 Mayıs 00:00 yerel = 11 Mayıs 00:00Z.
    [InlineData("Etc/UTC", "2025-05-10", "2025-05-10T23:59:59Z", true)]
    [InlineData("Etc/UTC", "2025-05-10", "2025-05-11T00:00:00Z", false)]
    // Europe/Berlin, yaz saatine geçiş (30 Mart +01:00 başlar, +02:00 biter): 31 Mart 00:00 yerel − 2 sa = 30 Mart 22:00Z.
    [InlineData("Europe/Berlin", "2025-03-30", "2025-03-30T21:59:59Z", true)]
    [InlineData("Europe/Berlin", "2025-03-30", "2025-03-30T22:00:00Z", false)]
    // Europe/Berlin, kış saatine dönüş (26 Ekim +02:00 başlar, +01:00 biter): 27 Ekim 00:00 yerel − 1 sa = 26 Ekim 23:00Z.
    [InlineData("Europe/Berlin", "2025-10-26", "2025-10-26T22:59:59Z", true)]
    [InlineData("Europe/Berlin", "2025-10-26", "2025-10-26T23:00:00Z", false)]
    // America/Santiago: 7 Eylül 2025 yerel 00:00 yaşanmaz (00:00 → 01:00; konteyner tzdata 2026e, IsInvalidTime ile bulundu).
    // 6 Eylül −04:00'te biter: 7 Eylül 00:00 yerel + 4 sa = 7 Eylül 04:00Z.
    [InlineData("America/Santiago", "2025-09-06", "2025-09-07T03:59:59Z", true)]
    [InlineData("America/Santiago", "2025-09-06", "2025-09-07T04:00:00Z", false)]
    // Atlantic/Azores, kış saatine dönüş (26 Ekim 01:00Z, +00:00 → −01:00; zdump, tzdata 2026e): 26 Ekim 00:00 yerel iki kez
    // yaşanır, ilki 00:00Z. Ofset gün sonundan alınmazsa belirsiz anın standart ofseti (−01:00) alınır, bitiş 01:00Z olur.
    [InlineData("Atlantic/Azores", "2025-10-25", "2025-10-25T23:59:59Z", true)]
    [InlineData("Atlantic/Azores", "2025-10-25", "2025-10-26T00:00:00Z", false)]
    public void SonGun_KurumunYerelGunuBitinceKapanir(string dilim, string sonGun, string an, bool acik) =>
        Assert.Equal(acik, Donem(sonGun: sonGun).IsWithdrawalOpen(Utc(an), TimeZoneInfo.FindSystemTimeZoneById(dilim)));

    // WithdrawalOverride takvimden önce gelir, dilimden bağımsızdır. Karşı örnek: zorlama yokken aynı anlar takvime uyar.
    [Theory]
    [InlineData("Europe/Istanbul")]
    [InlineData("Etc/UTC")]
    public void Zorlama_DilimdenBagimsizdir(string dilim)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(dilim);
        var (disari, iceri) = (Utc("2025-06-01T12:00:00Z"), Utc("2025-05-01T12:00:00Z"));

        Assert.Equal((true, false), (Donem(true).IsWithdrawalOpen(disari, tz), Donem(false).IsWithdrawalOpen(iceri, tz)));
        Assert.Equal((false, true), (Donem().IsWithdrawalOpen(disari, tz), Donem().IsWithdrawalOpen(iceri, tz)));
    }
}
