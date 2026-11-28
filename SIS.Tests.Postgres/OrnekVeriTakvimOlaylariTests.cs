using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;

namespace SIS.Tests.Postgres;

// Paket 35: örnek veri dönemlerine takvim üreticisiyle olay yazımı, testteki tek gün dışındaki yıllarda da
// hatasız ve dönem bugünden sonra başlamıyor (başlangıç pazartesiye çekilir; tatiller yıla göre kayar).
[Collection(PostgresKoleksiyonu.Ad)]
public sealed class OrnekVeriTakvimOlaylariTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.TemizleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(2028)] // 15 Eylül cuma
    [InlineData(2029)] // 15 Eylül cumartesi
    [InlineData(2030)] // 15 Eylül pazar, 15 Şubat cuma
    [InlineData(2033)]
    public async Task HerYilda_OlaylarYazilir_DonemPazartesiBaslar(int yil)
    {
        var donemler = new List<(int Yil, TermType Tur, DateTime Bas, DateTime Bit)>
        {
            (yil, TermType.Fall, Pazartesi(new DateTime(yil, 9, 15, 0, 0, 0, DateTimeKind.Utc)), new DateTime(yil + 1, 1, 10, 0, 0, 0, DateTimeKind.Utc)),
            (yil, TermType.Spring, Pazartesi(new DateTime(yil + 1, 2, 15, 0, 0, 0, DateTimeKind.Utc)), new DateTime(yil + 1, 6, 10, 0, 0, 0, DateTimeKind.Utc)),
        };
        await using (var db = fixture.YeniContext())
        {
            foreach (var d in donemler)
                db.AcademicTerms.Add(new AcademicTerm
                {
                    Name = $"{d.Yil} {d.Tur}", Year = d.Yil, TermType = d.Tur, TermStart = d.Bas, TermEnd = d.Bit,
                });
            await db.SaveChangesAsync();
            await SeedData.SeedTermCalendarEventsAsync(db, donemler);
        }

        await using var kontrol = fixture.YeniContext();
        var terimler = await kontrol.AcademicTerms.Include(t => t.CalendarEvents).ToListAsync();
        Assert.All(terimler, t =>
        {
            Assert.NotEmpty(t.CalendarEvents);
            Assert.Contains(t.CalendarEvents, e => e.EventType == CalendarEventType.CourseRegistration);
            Assert.Equal(donemler.Single(d => d.Tur == t.TermType).Bas, t.TermStart);
        });
    }

    private static DateTime Pazartesi(DateTime gun) => gun.AddDays(-(((int)gun.DayOfWeek + 6) % 7));
}
