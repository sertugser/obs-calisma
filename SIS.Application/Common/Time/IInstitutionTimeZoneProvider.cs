namespace SIS.Application.Common.Time;

/// <summary>
/// Kurumun saat dilimi (Sistem Ayarları'ndaki <c>Institution.TimeZone</c>). Takvim günü olarak saklanan
/// son günlerin hangi anda bittiğini belirler. Ayar geçersizse sessizce UTC'ye düşmez, hata verir.
/// </summary>
public interface IInstitutionTimeZoneProvider
{
    Task<TimeZoneInfo> GetAsync(CancellationToken ct = default);
}
