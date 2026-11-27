namespace SIS.Application.Common.Time;

/// <summary>
/// Kurum saat dilimi ayarı geçersiz. <see cref="InvalidOperationException"/>'dan türer: takvim uçları bunu
/// eskisi gibi 400'e eşler; çekilme kapısı yalnız bu türü yakalar. <see cref="Exception.Message"/> herkese
/// gösterilebilir genel cümledir (istisna iletisini dışarı taşıyan her yol için); kayıtlı değer ve ayar yolu
/// yalnız <see cref="Detail"/>'dadır ve yalnız günlüğe ve yönetici özetine yazılır.
/// </summary>
public sealed class InstitutionTimeZoneConfigurationException(string detail) : InvalidOperationException(PublicMessage)
{
    public const string PublicMessage =
        "Kurum saat dilimi ayarı geçersiz olduğu için bu işlem şu anda yapılamıyor. Lütfen sistem yöneticisine başvurun.";

    public string Detail { get; } = detail;
}
