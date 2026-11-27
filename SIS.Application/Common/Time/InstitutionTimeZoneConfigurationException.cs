namespace SIS.Application.Common.Time;

/// <summary>
/// Kurum saat dilimi ayarı geçersiz. <see cref="InvalidOperationException"/>'dan türer: takvim uçları bunu
/// eskisi gibi 400'e eşler; çekilme kapısı yalnız bu türü yakalar.
/// </summary>
public sealed class InstitutionTimeZoneConfigurationException(string message) : InvalidOperationException(message);
