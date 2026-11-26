using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using SIS.Application.Common.Services;
using SIS.Application.Common.Time;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Services;
using SIS.Infrastructure.Services.Time;
using SIS.Tests.TestDoubles;

namespace SIS.Tests.API.DersKaydi;

// Dersten çekilme modül kapısı, "Ak.Takvime Bağlı" (term-event-based) modunda dönemin çekilme kuralına
// (AcademicTerm.IsWithdrawalOpen) bağlıdır. Veri tohumdaki gibidir: çekilme olayı tek günlük, bitişi boş,
// 00:00 UTC; takvim özetindeki son gün (10 Mayıs) buna eşittir. Dönem 15 Şubat'ta başlar.
public class C06_CekilmeKapisiTests
{
    private static readonly SabitKurumSaatDilimi Istanbul = new("Europe/Istanbul");

    private static DateTime Utc(string s) =>
        DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    private static DersKaydiOrtami Ortam(string cekilmeModu = WebModuleControlModes.TermEventBased)
    {
        var o = new DersKaydiOrtami();
        o.EtkinDonem.CalendarEvents.Add(new TermCalendarEvent { EventType = CalendarEventType.CourseWithdrawal, Title = "Ders Çekilme Son Günü", StartDate = Utc("2025-05-10"), IsPublic = true });
        o.EtkinDonem.CalendarEvents.Add(new TermCalendarEvent { EventType = CalendarEventType.AddDrop, Title = "Ekle-Bırak", StartDate = Utc("2025-03-21"), EndDate = Utc("2025-03-28"), IsPublic = true });
        o.EtkinDonem.CalendarEvents.Add(new TermCalendarEvent { EventType = CalendarEventType.CourseRegistration, Title = "Ders Kayıt", StartDate = Utc("2025-03-01"), EndDate = Utc("2025-03-20"), IsPublic = true });
        foreach (var (modul, mod) in new[] { ("StudentWithdrawal", cekilmeModu), ("StudentAddDrop", WebModuleControlModes.TermEventBased), ("StudentCourseRegistration", WebModuleControlModes.TermEventBased) })
            o.Db.WebModuleSettings.Add(new WebModuleSetting { ModuleKey = modul, Key = "Mode", Value = mod });
        o.Db.SaveChanges();
        return o;
    }

    private static WebModuleSettingsProvider Ayarlar(DersKaydiOrtami o) => new(o.Db, new MemoryCache(new MemoryCacheOptions()), o.Saat);

    private static WebModuleControlService Kapi(DersKaydiOrtami o, IInstitutionTimeZoneProvider dilim) => new(o.Db, Ayarlar(o), o.Saat, dilim);

    private static CourseRegistrationService Servis(DersKaydiOrtami o, IInstitutionTimeZoneProvider dilim) =>
        new(o.Db, o.Dogrulama(), o.Saat, webSettings: Ayarlar(o), webModuleControls: Kapi(o, dilim), institutionTimeZone: dilim);

    // Sistem ayarlarında geçersiz kurum saat dilimi: gerçek sağlayıcı hata verir.
    private static InstitutionTimeZoneProvider BozukDilim(DersKaydiOrtami o)
    {
        o.Db.SystemConfigs.Add(new SystemConfig { Key = InstitutionTimeZoneValidator.ConfigKey, Value = "Mars/Olympus", DataType = "string" });
        o.Db.SaveChanges();
        return new InstitutionTimeZoneProvider(new SystemConfigProvider(o.Db, new MemoryCache(new MemoryCacheOptions())));
    }

    // İstanbul (+03:00): son gün 11 Mayıs 00:00 yerel = 10 Mayıs 21:00Z'de biter. Etc/UTC: 11 Mayıs 00:00Z.
    [Theory]
    [InlineData("Europe/Istanbul", "2025-02-14T23:59:59Z", false)]
    [InlineData("Europe/Istanbul", "2025-02-15T00:00:00Z", true)]
    [InlineData("Europe/Istanbul", "2025-05-09T21:00:00Z", true)]
    [InlineData("Europe/Istanbul", "2025-05-10T09:00:00Z", true)]
    [InlineData("Europe/Istanbul", "2025-05-10T20:59:59Z", true)]
    [InlineData("Europe/Istanbul", "2025-05-10T21:00:00Z", false)]
    [InlineData("Etc/UTC", "2025-05-10T23:59:59Z", true)]
    [InlineData("Etc/UTC", "2025-05-11T00:00:00Z", false)]
    public async Task Kapi_DonemBasindanSonGunSonunaKadarAciktir(string dilim, string an, bool acik)
    {
        using var o = Ortam();
        o.Saat.UtcNow = Utc(an);

        var sonuc = await Kapi(o, new SabitKurumSaatDilimi(dilim)).EvaluateAsync("student-withdrawal");

        Assert.Equal((acik, acik ? "Açık" : "Takvime Bağlı"), (sonuc.IsOpen, sonuc.EffectiveStateLabel));
    }

    // Uçtan uca: tohumlanmış modla son gün 09:00Z'de çekilme kabul edilir, 21:00Z'de modül kapısında reddedilir.
    [Theory]
    [InlineData("2025-05-10T09:00:00Z", true)]
    [InlineData("2025-05-10T21:00:00Z", false)]
    public async Task Cekilme_SonGunKurumSaatiyleGunSonunaKadarKabulEdilir(string an, bool kabul)
    {
        using var o = Ortam();
        var (a, _) = (o.AktifKayitEkle(o.DersEkle("AAA101")), o.AktifKayitEkle(o.DersEkle("BBB101")));
        o.Saat.UtcNow = Utc(an);

        var y = await Servis(o, Istanbul).WithdrawCourseAsync(o.Ogrenci.Id, a.Id);

        Assert.Equal(kabul, y.Success);
        Assert.Equal(kabul ? RegistrationStatus.Withdrawn : RegistrationStatus.Active, o.Db.CourseRegistrations.Single(r => r.Id == a.Id).Status);
        if (!kabul)
            Assert.Equal("WebModuleWithdrawalClosed", Assert.Single(y.ValidationResult!.Errors).Rule);
    }

    // Öğrencinin gördüğü rozet (takvim DTO'su) ile kapı aynı anda aynı cevabı verir.
    [Theory]
    [InlineData("2025-05-09T12:00:00Z", true)]
    [InlineData("2025-05-10T20:30:00Z", true)]
    [InlineData("2025-05-10T21:30:00Z", false)]
    public async Task Rozet_VeKapi_AyniCevabiVerir(string an, bool acik)
    {
        using var o = Ortam();
        o.Saat.UtcNow = Utc(an);

        var rozet = (await new AcademicCalendarService(o.Db, o.Saat, Istanbul).GetActiveStudentViewAsync())!.IsWithdrawalOpen;
        var kapi = (await Kapi(o, Istanbul).EvaluateAsync("student-withdrawal")).IsOpen;

        Assert.Equal((acik, acik), (rozet, kapi));
    }

    // Bozuk saat dilimi ayarı: modül listesi döner, çekilme kapalı ve nedeni (duyuru metnine rağmen) yazılı; ders kaydı kapısı
    // etkilenmez; çekilme isteği bu nedenle reddedilir.
    [Fact]
    public async Task BozukSaatDilimi_ListeCokmez_CekilmeKapaliVeNedenli_OtekiKapilarEtkilenmez()
    {
        using var o = Ortam();
        var (a, _) = (o.AktifKayitEkle(o.DersEkle("AAA101")), o.AktifKayitEkle(o.DersEkle("BBB101")));
        var bozuk = BozukDilim(o);
        o.Db.WebModuleSettings.Add(new WebModuleSetting { ModuleKey = "StudentWithdrawal", Key = "Announcement", Value = "Çekilme yalnız çekilme penceresinde açıktır." });
        o.Db.SaveChanges();
        o.Saat.UtcNow = Utc("2025-03-10T12:00:00Z");

        var liste = await Kapi(o, bozuk).GetSummariesAsync();

        var cekilme = liste.Single(m => m.Code == "student-withdrawal");
        Assert.Equal((false, "Ayar Hatası"), (cekilme.IsCurrentlyOpen, cekilme.EffectiveStateLabel));
        Assert.Contains("Kurum saat dilimi ayarı geçersiz", cekilme.EffectiveMessage);
        Assert.True(liste.Single(m => m.Code == "student-course-registration").IsCurrentlyOpen);
        Assert.True((await Kapi(o, bozuk).EvaluateAsync("student-course-registration")).IsOpen);

        var y = await Servis(o, bozuk).WithdrawCourseAsync(o.Ogrenci.Id, a.Id);
        var hata = Assert.Single(y.ValidationResult!.Errors);
        Assert.Equal("WebModuleWithdrawalClosed", hata.Rule);
        Assert.Contains("Kurum saat dilimi ayarı geçersiz", hata.Message);
    }

    // "Açık" ve "Kapalı" modları takvimden ve kurum saat diliminden bağımsızdır (bozuk ayar bile okunmaz).
    [Theory]
    [InlineData(WebModuleControlModes.Open, "2025-06-01T12:00:00Z", true)]
    [InlineData(WebModuleControlModes.Closed, "2025-05-10T09:00:00Z", false)]
    public async Task AcikVeKapaliModlar_Degismez(string mod, string an, bool acik)
    {
        using var o = Ortam(mod);
        o.Saat.UtcNow = Utc(an);

        var sonuc = await Kapi(o, BozukDilim(o)).EvaluateAsync("student-withdrawal");

        Assert.Equal((acik, acik ? "Açık" : "Kapalı"), (sonuc.IsOpen, sonuc.EffectiveStateLabel));
    }

    // Öteki modüller takvim olayının kendi penceresine bakmaya devam eder: çekilme açıkken ekle-bırak kapalıdır.
    [Theory]
    [InlineData("2025-03-25T12:00:00Z", true)]
    [InlineData("2025-05-09T12:00:00Z", false)]
    public async Task EkleBirak_OlayPenceresineBagliKalir(string an, bool acik)
    {
        using var o = Ortam();
        o.Saat.UtcNow = Utc(an);

        Assert.Equal(acik, (await Kapi(o, Istanbul).EvaluateAsync("student-add-drop")).IsOpen);
    }
}
