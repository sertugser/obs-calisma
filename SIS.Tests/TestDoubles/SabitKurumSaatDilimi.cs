using SIS.Application.Common.Time;

namespace SIS.Tests.TestDoubles;

/// <summary>Testte kurum saat dilimini açıkça veren ikame (IANA adıyla).</summary>
public sealed class SabitKurumSaatDilimi(string ianaAdi) : IInstitutionTimeZoneProvider
{
    public Task<TimeZoneInfo> GetAsync(CancellationToken ct = default) =>
        Task.FromResult(TimeZoneInfo.FindSystemTimeZoneById(ianaAdi));
}
