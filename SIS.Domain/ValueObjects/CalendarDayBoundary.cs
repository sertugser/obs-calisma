namespace SIS.Domain.ValueObjects;

/// <summary>
/// Takvim günü olarak saklanan bir tarihin kurumun yerel saatinde bittiği an (UTC).
/// Gün D, saklanan değerin UTC tarihidir; bitiş = (D+1 yerel 00:00) − o andan hemen önceki ofset.
/// Ofset gün sonundan alındığı için yaz saati geçiş günlerinde ve yerel gece yarısının hiç yaşanmadığı
/// günlerde de doğru an bulunur.
/// </summary>
public static class CalendarDayBoundary
{
    public static DateTime EndUtc(DateTime storedDay, TimeZoneInfo timeZone)
    {
        var nextDay = DateTime.SpecifyKind(storedDay.Date.AddDays(1), DateTimeKind.Unspecified);
        var endUtc = nextDay - timeZone.GetUtcOffset(nextDay.AddTicks(-1));
        return DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
    }
}
