namespace SIS.Application.Common.Time;

/// <summary>
/// Kurum saat dilimi ayarının tek kuralı: boş olmayan, bu sunucuda bulunan bir IANA adı (Bölge/Şehir).
/// Ayar ekranı da saat dilimi sağlayıcısı da bu doğrulayıcıyı kullanır.
/// </summary>
public static class InstitutionTimeZoneValidator
{
    public const string ConfigKey = "Institution.TimeZone";
    public const string DefaultId = "Etc/UTC";

    public static bool TryResolve(string? id, out TimeZoneInfo timeZone, out string error)
    {
        TimeZoneInfo? found = null;
        error = string.IsNullOrWhiteSpace(id) ? "Kurum saat dilimi boş olamaz."
            : !TimeZoneInfo.TryFindSystemTimeZoneById(id, out found) ? $"'{id}' saat dilimi bu sunucuda bulunamadı."
            : !found.HasIanaId ? $"'{id}' bir IANA saat dilimi adı değil; Bölge/Şehir biçiminde girilmelidir (ör. Etc/UTC)."
            : string.Empty;
        timeZone = error.Length == 0 ? found! : TimeZoneInfo.Utc;
        return error.Length == 0;
    }
}
