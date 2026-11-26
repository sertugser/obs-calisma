using SIS.Application.Common.Services;
using SIS.Application.Common.Time;

namespace SIS.Infrastructure.Services.Time;

/// <summary>Kurum saat dilimini sistem ayarından okur; önbelleği ayar sağlayıcısınındır.</summary>
public sealed class InstitutionTimeZoneProvider(ISystemConfigProvider config) : IInstitutionTimeZoneProvider
{
    public async Task<TimeZoneInfo> GetAsync(CancellationToken ct = default)
    {
        var id = await config.GetStringAsync(
            InstitutionTimeZoneValidator.ConfigKey, InstitutionTimeZoneValidator.DefaultId, ct);

        if (InstitutionTimeZoneValidator.TryResolve(id, out var timeZone, out var error))
            return timeZone;

        throw new InvalidOperationException(
            $"Kurum saat dilimi ayarı geçersiz: {error} Sistem Ayarları > Genel bölümünden geçerli bir saat dilimi seçin.");
    }
}
