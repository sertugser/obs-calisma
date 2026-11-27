using SIS.Domain.Enums;
using SIS.Domain.ValueObjects;

namespace SIS.Domain.Entities;

public class AcademicTerm
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public string ShortEnglishName { get; set; } = string.Empty;
    public string? OtherName { get; set; }

    public int Year { get; set; }
    public TermType TermType { get; set; } = TermType.Fall;

    // Dönem kimliğini tanımlayan 2 tarih burada kalır
    public DateTime TermStart { get; set; }
    public DateTime TermEnd { get; set; }

    // Admin override alanları (Faz 4 — manuel kontrol):
    // OverrideStart: admin dönemi TermStart'tan önce aktif etmek istiyorsa (erken aktif).
    // OverrideEnd:   admin dönemi TermEnd'den sonra aktif tutmak istiyorsa (uzatma).
    // Null ise dönem tarihi kullanılır; dolu ise override tarih kazanır (effStart/effEnd).
    public DateTime? OverrideStart { get; set; }
    public DateTime? OverrideEnd { get; set; }

    // SİLİNDİ (Faz 2): RegistrationStart/End, AddDropStart/End, WithdrawalDeadline,
    //                  GradeEntryStart/End + 4 override — TermAcademicCalendar'a taşındı

    // Varsayılan KAPALI: yeni dönem otomatik aktif olmaz; admin "Kullanıma Aç" ile açar.
    // Tek-aktif değişmezi AdminTermService.ActivateTermAsync'te korunur (Proliz/Banner modeli).
    public bool IsActivated { get; set; } = false;
    public bool IsCancelled { get; set; } = false;
    public YoksisStatus YoksisStatus { get; set; } = YoksisStatus.Inactive;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation'lar
    public TermAcademicCalendar? AcademicCalendar { get; set; }
    public ICollection<TermCalendarEvent> CalendarEvents { get; set; } = new List<TermCalendarEvent>();

    // ── Computed ─────────────────────────────────────────────────────

    /// <summary>
    /// Override tarihleri ile efektif başlangıç. OverrideStart varsa onu, yoksa TermStart döner.
    /// </summary>
    public DateTime EffectiveStart => OverrideStart ?? TermStart;

    /// <summary>
    /// Override tarihleri ile efektif bitiş. OverrideEnd varsa onu, yoksa TermEnd döner.
    /// </summary>
    public DateTime EffectiveEnd => OverrideEnd ?? TermEnd;

    // Aktiflik manuel "Kullanıma Aç" bayrağına bağlıdır (Proliz/Banner modeli) —
    // tarih aralığı (TermStart/TermEnd) artık aktifliği BELİRLEMEZ. İşlem pencereleri
    // (kayıt/ek-bırak/not girişi) ayrıca faz metotlarındaki takvim tarihleriyle kapılanır.
    // 'now' parametresi imza uyumluluğu için korunur (faz metotları kullanır).
    public TermStatus GetStatus(DateTime now) =>
        IsCancelled ? TermStatus.Cancelled
        : IsActivated ? TermStatus.Active
        : TermStatus.Inactive;

    public bool IsActive(DateTime now) => GetStatus(now) == TermStatus.Active;

    // Phase method'ları: AcademicCalendar null ise false döner (null-safe, mevcut davranışla uyumlu).
    // Çağıran tarafın Include'ı şart; aksi EF lazy olmadığından AcademicCalendar null görünür
    // ve false dönüşü kullanıcıya "kapalı" olarak yansır — veri tutarsızlığı sessiz kalmaz.
    public bool IsRegistrationOpen(DateTime now) =>
        IsActive(now) && AcademicCalendar is { } cal &&
        (cal.RegistrationOverride ?? (now >= cal.RegistrationStart && now <= cal.RegistrationEnd));

    public bool IsAddDropOpen(DateTime now) =>
        IsActive(now) && AcademicCalendar is { } cal &&
        (cal.AddDropOverride ?? (now >= cal.AddDropStart && now <= cal.AddDropEnd));

    // Çekilme ekle-bırak döneminin son günü (AddDropEnd, takvim günü) kurumun yerel günü bitince açılır; dönem başından
    // önce açılmaz (ekle-bırak bitişi boşsa ya da dönem başından önceyse dönem başı). Son gün (WithdrawalDeadline, takvim
    // günü) kurumun yerel gününün sonuna kadar açıktır.
    public bool IsWithdrawalOpen(DateTime nowUtc, TimeZoneInfo institutionTimeZone) =>
        IsActive(nowUtc) && AcademicCalendar is { } cal &&
        (cal.WithdrawalOverride ?? (nowUtc >= WithdrawalStartUtc(cal, institutionTimeZone)
            && nowUtc < CalendarDayBoundary.EndUtc(cal.WithdrawalDeadline, institutionTimeZone)));

    private DateTime WithdrawalStartUtc(TermAcademicCalendar cal, TimeZoneInfo institutionTimeZone)
    {
        var addDropOverUtc = CalendarDayBoundary.EndUtc(cal.AddDropEnd, institutionTimeZone);
        return addDropOverUtc > TermStart ? addDropOverUtc : TermStart;
    }

    public bool IsGradeEntryOpen(DateTime now) =>
        IsActive(now) && AcademicCalendar is { } cal &&
        (cal.GradeEntryOverride ?? (now >= cal.GradeEntryStart && now <= cal.GradeEntryEnd));
}
