using System.Globalization;
using SIS.Domain.Entities;

namespace SIS.Tests.Domain;

// Çekilme ekle-bırak döneminin son günü kurumun yerel günü bitince açılır (ekle-bırak son günü boyunca kapalı), son günün
// yerel gün sonunda kapanır. Dönem 15 Şubat'ta başlar; son gün 10 Mayıs.
public class CekilmeBaslangiciTests
{
    private static AcademicTerm Donem(string? ekleBirakBitisi, bool? zorla = null) => new()
    {
        IsActivated = true, TermStart = Utc("2025-02-15"), TermEnd = Utc("2025-06-15"),
        AcademicCalendar = new TermAcademicCalendar
        {
            AddDropEnd = ekleBirakBitisi is null ? default : Utc(ekleBirakBitisi),
            WithdrawalDeadline = Utc("2025-05-10"), WithdrawalOverride = zorla
        }
    };

    private static DateTime Utc(string s) =>
        DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    private static bool Acik(AcademicTerm donem, string an, string dilim = "Europe/Istanbul") =>
        donem.IsWithdrawalOpen(Utc(an), TimeZoneInfo.FindSystemTimeZoneById(dilim));

    [Theory]
    // Europe/Istanbul (+03:00): ekle-bırak son günü 28 Şubat, 1 Mart 00:00 yerel = 28 Şubat 21:00Z'de biter.
    [InlineData("Europe/Istanbul", "2025-02-27T12:00:00Z", false)]
    [InlineData("Europe/Istanbul", "2025-02-28T12:00:00Z", false)]
    [InlineData("Europe/Istanbul", "2025-02-28T20:59:59Z", false)]
    [InlineData("Europe/Istanbul", "2025-02-28T21:00:00Z", true)]
    [InlineData("Europe/Istanbul", "2025-05-10T20:59:59Z", true)]
    [InlineData("Europe/Istanbul", "2025-05-10T21:00:00Z", false)]
    // Etc/UTC: 1 Mart 00:00Z.
    [InlineData("Etc/UTC", "2025-02-28T23:59:59Z", false)]
    [InlineData("Etc/UTC", "2025-03-01T00:00:00Z", true)]
    public void EkleBirakBitinceAcilir_SonGunSonundaKapanir(string dilim, string an, bool acik) =>
        Assert.Equal(acik, Acik(Donem("2025-02-28"), an, dilim));

    // Ekle-bırak bitişi boşsa ya da dönem başından önceyse (ör. kayıt bitişinden türetilmiş) pencere dönem başında açılır.
    [Theory]
    [InlineData(null, "2025-02-14T23:59:59Z", false)]
    [InlineData(null, "2025-02-15T00:00:00Z", true)]
    [InlineData("2025-02-10", "2025-02-14T23:59:59Z", false)]
    [InlineData("2025-02-10", "2025-02-15T00:00:00Z", true)]
    public void EkleBirakBitisiYoksaVeyaDonemdenOnceyse_DonemBasindanAcik(string? ekleBirakBitisi, string an, bool acik) =>
        Assert.Equal(acik, Acik(Donem(ekleBirakBitisi), an));

    // Zorlama takvimden önce gelir: true ekle-bırak içinde açar, false pencere içinde kapatır.
    [Theory]
    [InlineData(true, "2025-02-20T12:00:00Z", true)]
    [InlineData(false, "2025-04-01T12:00:00Z", false)]
    public void Zorlama_EkleBirakBaslangicindanOnceGelir(bool zorla, string an, bool acik) =>
        Assert.Equal(acik, Acik(Donem("2025-02-28", zorla), an));
}
