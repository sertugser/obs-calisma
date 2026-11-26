namespace SIS.Application.Common.Time;

/// <summary>
/// Kurum saat dilimi ayarının tek kuralı: boş olmayan, bu sunucuda bulunan bir IANA adı (Bölge/Şehir).
/// Ayar ekranı da saat dilimi sağlayıcısı da bu doğrulayıcıyı kullanır.
/// </summary>
public static class InstitutionTimeZoneValidator
{
    public const string ConfigKey = "Institution.TimeZone";
    public const string DefaultId = "Etc/UTC";

    // UTC sunucunun saat dilimi verisine (tzdata) bakılmadan çözülür; varsayılan kurulum tzdata gerektirmez.
    private static readonly TimeZoneInfo EtcUtc = TimeZoneInfo.CreateCustomTimeZone(DefaultId, TimeSpan.Zero, DefaultId, DefaultId);

    // Sunucunun kendi dilimine ya da tzdata iç dosyalarına işaret eden adlar açık bir seçim değildir.
    private static readonly string[] ServerBoundNames = ["localtime", "posixrules", "Factory"];
    private static readonly string[] ServerBoundPrefixes = ["right/", "posix/"];

    public static bool TryResolve(string? id, out TimeZoneInfo timeZone, out string error)
    {
        TimeZoneInfo? found = null;
        error = string.IsNullOrWhiteSpace(id) ? "Kurum saat dilimi boş olamaz."
            : IsServerBound(id) ? $"'{id}' kurum saat dilimi olarak kullanılamaz; Bölge/Şehir biçiminde girilmelidir (ör. Europe/Istanbul)."
            : (found = Find(id)) is null ? $"'{id}' saat dilimi bu sunucuda bulunamadı."
            : !found.HasIanaId && found != EtcUtc ? $"'{id}' bir IANA saat dilimi adı değil; Bölge/Şehir biçiminde girilmelidir (ör. Etc/UTC)."
            : string.Empty;
        timeZone = error.Length == 0 ? found! : TimeZoneInfo.Utc;
        return error.Length == 0;
    }

    private static bool IsServerBound(string id) =>
        ServerBoundNames.Any(name => string.Equals(id, name, StringComparison.OrdinalIgnoreCase))
        || ServerBoundPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    // Dosya adı büyük-küçük harfe duyarlı sistemlerde de ad, sunucunun dilim listesinden normal biçimine çevrilir.
    private static TimeZoneInfo? Find(string id) =>
        string.Equals(id, "UTC", StringComparison.OrdinalIgnoreCase) || string.Equals(id, DefaultId, StringComparison.OrdinalIgnoreCase) ? EtcUtc
        : TimeZoneInfo.TryFindSystemTimeZoneById(id, out var exact) ? exact
        : TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(zone => string.Equals(zone.Id, id, StringComparison.OrdinalIgnoreCase));
}
