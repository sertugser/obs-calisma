using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using System.Globalization;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static async Task SeedTermsAsync(AppDbContext context)
    {
        // Academic Terms
        if (!context.AcademicTerms.Any())
        {
            var terms = new (string Name, string ShortName, string EnglishName, int Year, TermType TermType, string RegStart, string RegEnd, string AddStart, string AddEnd, string TermStart, string TermEnd, string WithdrawalDeadline, bool IsCancelled)[]
            {
                ("2020-2021 Güz",   "20-21 Güz",   "Fall 2020-2021",   2020, TermType.Fall,   "2020-09-14", "2020-09-28", "2020-09-29", "2020-10-12", "2020-09-28", "2021-01-15", "2020-11-16", false),
                ("2020-2021 Bahar", "20-21 Bahar", "Spring 2020-2021", 2020, TermType.Spring, "2021-02-01", "2021-02-15", "2021-02-16", "2021-02-28", "2021-02-15", "2021-06-15", "2021-04-05", false),
                ("2021-2022 Güz",   "21-22 Güz",   "Fall 2021-2022",   2021, TermType.Fall,   "2021-09-13", "2021-09-27", "2021-09-28", "2021-10-11", "2021-09-27", "2022-01-14", "2021-11-15", false),
                ("2021-2022 Bahar", "21-22 Bahar", "Spring 2021-2022", 2021, TermType.Spring, "2022-02-07", "2022-02-21", "2022-02-22", "2022-03-07", "2022-02-21", "2022-06-17", "2022-04-11", false),
                ("2022-2023 Güz",   "22-23 Güz",   "Fall 2022-2023",   2022, TermType.Fall,   "2022-09-12", "2022-09-26", "2022-09-27", "2022-10-10", "2022-09-26", "2023-01-13", "2022-11-14", false),
                ("2022-2023 Bahar", "22-23 Bahar", "Spring 2022-2023", 2022, TermType.Spring, "2023-02-06", "2023-02-20", "2023-02-21", "2023-03-06", "2023-02-20", "2023-06-16", "2023-04-10", false),
                ("2023-2024 Güz",   "23-24 Güz",   "Fall 2023-2024",   2023, TermType.Fall,   "2023-09-01", "2023-09-15", "2023-09-16", "2023-09-30", "2023-09-16", "2024-01-10", "2023-11-04", false),
                ("2023-2024 Bahar", "23-24 Bahar", "Spring 2023-2024", 2023, TermType.Spring, "2024-02-01", "2024-02-15", "2024-02-16", "2024-02-28", "2024-02-16", "2024-06-15", "2024-04-05", false),
                ("2024-2025 Güz",   "24-25 Güz",   "Fall 2024-2025",   2024, TermType.Fall,   "2024-09-01", "2024-09-15", "2024-09-16", "2024-09-30", "2024-09-16", "2025-01-10", "2024-11-04", false),
                ("2024-2025 Bahar", "24-25 Bahar", "Spring 2024-2025", 2024, TermType.Spring, "2025-02-01", "2025-02-15", "2025-02-16", "2025-02-28", "2025-02-16", "2025-06-15", "2025-04-07", false),
                ("2025-2026 Güz",   "25-26 Güz",   "Fall 2025-2026",   2025, TermType.Fall,   "2025-09-15", "2025-09-19", "2025-09-29", "2025-10-03", "2025-09-22", "2026-01-02", "2025-11-07", false),
                ("2025-2026 Bahar", "25-26 Bahar", "Spring 2025-2026", 2025, TermType.Spring, "2026-02-16", "2026-02-20", "2026-03-02", "2026-03-06", "2026-02-23", "2026-06-05", "2026-04-03", false),
                ("2025-2026 Yaz",   "25-26 Yaz",   "Summer 2025-2026", 2025, TermType.Summer, "2026-06-30", "2026-07-03", "2026-06-30", "2026-07-03", "2026-07-13", "2026-08-28", "2026-08-28", false),
            };

            foreach (var t in terms)
            {
                var termEnd = ParseUtcDate(t.TermEnd);
                var regStart = ParseUtcDate(t.RegStart);
                var regEnd = ParseUtcDate(t.RegEnd);
                var addStart = ParseUtcDate(t.AddStart);
                var addEnd = ParseUtcDate(t.AddEnd);
                var withdrawal = ParseUtcDate(t.WithdrawalDeadline);
                var term = new AcademicTerm
                {
                    Name = t.Name,
                    ShortName = t.ShortName,
                    EnglishName = t.EnglishName,
                    Year = t.Year,
                    TermType = t.TermType,
                    TermStart = ParseUtcDate(t.TermStart),
                    TermEnd = termEnd,
                    IsCancelled = t.IsCancelled,
                    YoksisStatus = YoksisStatus.Inactive,
                };

                // Seed yalnızca Fall 2024-2025 dönemine otomatik takvim ekler;
                // diğer dönemlere admin ihtiyaç duydukça kendi takvimini oluşturur.
                if (t.Year == 2024 && t.TermType == TermType.Fall)
                {
                    term.AcademicCalendar = new SIS.Domain.Entities.TermAcademicCalendar
                    {
                        RegistrationStart = regStart,
                        RegistrationEnd = regEnd,
                        AddDropStart = addStart,
                        AddDropEnd = addEnd,
                        WithdrawalDeadline = withdrawal,
                        GradeEntryStart = termEnd.AddDays(-14),
                        GradeEntryEnd = termEnd.AddDays(14),
                        CreatedAt = DateTime.UtcNow,
                    };
                }

                context.AcademicTerms.Add(term);
            }
            await context.SaveChangesAsync();
        }
    }

    // Derslikler kurum verisidir (Kurum/YerelKurumVeriKaynagi).
    internal static async Task SeedClassroomsAsync(AppDbContext context)
    {
        // Classrooms
        if (!context.Classrooms.Any())
        {
            var classrooms = new (string Name, int Capacity, ClassroomType Type)[]
            {
                ("AMFİ 1", 110, ClassroomType.Lecture),
                ("Amfi - Sınıf-Yeni-Kat Sıfır", 80, ClassroomType.Lecture),
                ("C 017", 70, ClassroomType.Lecture),
                ("C 018", 70, ClassroomType.Lecture),
                ("C 025", 70, ClassroomType.Lecture),
                ("C 026", 70, ClassroomType.Lecture),
                ("C 036", 80, ClassroomType.Lecture),
                ("C 054", 30, ClassroomType.Lecture),
                ("C 005", 50, ClassroomType.Lecture),
                ("L139 Ceng Lab-1", 40, ClassroomType.Lab),
                ("L141 Ceng Lab-2", 40, ClassroomType.Lab),
                ("Ceng Lab-3 (Kat 0)", 40, ClassroomType.Lab),
                ("L140 EEE LAB", 40, ClassroomType.Lab),
                ("L143 PHY LAB", 40, ClassroomType.Lab),
            };

            foreach (var (name, capacity, type) in classrooms)
            {
                context.Classrooms.Add(new Classroom { Name = name, Capacity = capacity, Type = type });
            }
            await context.SaveChangesAsync();
        }
    }

    internal static async Task SeedCalendarEventsAsync(AppDbContext context)
    {
        if (context.TermCalendarEvents.Any())
            return;

        var springTerm = await context.AcademicTerms
            .FirstOrDefaultAsync(t => t.Year == 2025 && t.TermType == TermType.Spring);
        var fallTerm = await context.AcademicTerms
            .FirstOrDefaultAsync(t => t.Year == 2025 && t.TermType == TermType.Fall);
        var summerTerm = await context.AcademicTerms
            .FirstOrDefaultAsync(t => t.Year == 2025 && t.TermType == TermType.Summer);

        if (springTerm is null)
            return;

        var events = new[]
        {
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.GraduateApplication,  Title = "Lisansüstü Program Başvuruları",          StartDate = Utc(2025, 11, 24), EndDate = Utc(2026, 2, 12),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.HorizontalTransfer,   Title = "Yatay Geçiş Başvuruları",                 StartDate = Utc(2026,  1, 26), EndDate = Utc(2026, 1, 30),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.DoubleMajor,          Title = "Çift Anadal / Yan Dal Başvuruları",       StartDate = Utc(2026,  1, 26), EndDate = Utc(2026, 1, 30),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.TuitionPayment,       Title = "Harç Ödeme",                              StartDate = Utc(2026,  1, 26), EndDate = Utc(2026, 2, 13),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.CourseRegistration,   Title = "Ders Kayıt ve Danışman Onayları",         StartDate = Utc(2026,  2, 16), EndDate = Utc(2026, 2, 20),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.ClassesStart,         Title = "Derslerin Başlangıcı",                    StartDate = Utc(2026,  2, 23), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.AddDrop,              Title = "Ders Ekle / Bırak",                       StartDate = Utc(2026,  3,  2), EndDate = Utc(2026, 3,  6),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.ReligiousHoliday,     Title = "Ramazan Bayramı Tatili",                  StartDate = Utc(2026,  3, 19), EndDate = Utc(2026, 3, 22),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.CourseWithdrawal,     Title = "Ders Çekilme Son Günü",                   StartDate = Utc(2026,  4,  3), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.Other,                Title = "Kayıt Dondurma Başvurusu Son Günü",       StartDate = Utc(2026,  4,  3), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MidtermExam,          Title = "Ara Sınavlar",                            StartDate = Utc(2026,  4,  6), EndDate = Utc(2026, 4, 11),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MidtermExam,          Title = "Ara Sınav Mazeret Sınavları",             StartDate = Utc(2026,  4, 15), EndDate = Utc(2026, 4, 17),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.NationalHoliday,      Title = "Ulusal Egemenlik ve Çocuk Bayramı",       StartDate = Utc(2026,  4, 23), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MidtermGradePublication, Title = "Ara Sınav Notlarının Yayımlanması",    StartDate = Utc(2026,  4, 24), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.NationalHoliday,      Title = "Emek ve Dayanışma Günü",                  StartDate = Utc(2026,  5,  1), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.NationalHoliday,      Title = "Atatürk'ü Anma, Gençlik ve Spor Bayramı", StartDate = Utc(2026, 5, 19), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.ReligiousHoliday,     Title = "Kurban Bayramı Tatili",                   StartDate = Utc(2026,  5, 26), EndDate = Utc(2026, 5, 30),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.ClassesEnd,           Title = "Derslerin Sonu",                          StartDate = Utc(2026,  6,  5), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.FinalExam,            Title = "Final Sınavları",                         StartDate = Utc(2026,  6,  8), EndDate = Utc(2026, 6, 19),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.Other,                Title = "Hazırlık Muafiyet Sınavları",             StartDate = Utc(2026,  6, 15), EndDate = Utc(2026, 6, 18),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.FinalGradePublication, Title = "Final Notlarının Yayımlanması",          StartDate = Utc(2026,  6, 24), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MakeupExam,           Title = "Bütünleme Sınavı Başvuru Son Günü",       StartDate = Utc(2026,  6, 26), EndDate = null,              IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MakeupExam,           Title = "Bütünleme Sınavları",                     StartDate = Utc(2026,  6, 29), EndDate = Utc(2026, 7,  4),  IsPublic = true },
            new TermCalendarEvent { TermId = springTerm.Id, EventType = CalendarEventType.MakeupGradePublication, Title = "Bütünleme Notlarının Yayımlanması",    StartDate = Utc(2026,  7,  8), EndDate = null,              IsPublic = true },
        };

        foreach (var ev in events)
        {
            ev.IsGenerated = true;
            ev.ChainCode = "SEED";
            ev.BlocksAcademicEvents = ev.EventType is CalendarEventType.NationalHoliday or CalendarEventType.ReligiousHoliday or CalendarEventType.ImportantDay;
        }

        context.TermCalendarEvents.AddRange(events);

        if (fallTerm is not null)
        {
            context.TermCalendarEvents.AddRange(
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.TuitionPayment, Title = "Erken Odemeler", StartDate = Utc(2025, 6, 25), EndDate = Utc(2025, 7, 4), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_early_payment" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.InternationalStudentApplication, Title = "Uluslararasi Ogrenci Basvuru ve Kayitlari", StartDate = Utc(2025, 7, 1), EndDate = Utc(2025, 11, 28), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_international" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.NationalHoliday, Title = "Demokrasi ve Milli Birlik Gunu", StartDate = Utc(2025, 7, 15), IsPublic = true, IsGenerated = true, ChainCode = "HOLIDAY", BlocksAcademicEvents = true },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.HorizontalTransfer, Title = "Yatay Gecis Basvurulari", StartDate = Utc(2025, 8, 1), EndDate = Utc(2025, 8, 15), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_transfer_app" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.DoubleMajor, Title = "Cift Anadal ve Yandal Programi Basvurulari", StartDate = Utc(2025, 8, 1), EndDate = Utc(2025, 8, 15), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_double_major" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.GraduateApplication, Title = "Lisansustu Programlari Basvurulari", StartDate = Utc(2025, 8, 4), EndDate = Utc(2025, 9, 11), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_graduate_app" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.TuitionPayment, Title = "Ogrenim Ucretlerinin Odenmesi", StartDate = Utc(2025, 8, 4), EndDate = Utc(2025, 8, 15), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_tuition" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.TransferEvaluation, Title = "Yatay Gecis Basvurularinin Degerlendirilmesi", StartDate = Utc(2025, 8, 18), EndDate = Utc(2025, 9, 1), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_transfer_eval" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.NationalHoliday, Title = "Zafer Bayrami", StartDate = Utc(2025, 8, 30), IsPublic = true, IsGenerated = true, ChainCode = "HOLIDAY", BlocksAcademicEvents = true },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.TransferResult, Title = "Yatay Gecis, Cift Anadal ve Yandal Sonuclarinin Ilani", StartDate = Utc(2025, 9, 5), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_transfer_result" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.TransferRegistration, Title = "Yatay Gecis Kabul Edilen Ogrencilerin Kayitlari", StartDate = Utc(2025, 9, 8), EndDate = Utc(2025, 9, 12), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_transfer_registration_main" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.PrepSchoolExam, Title = "Hazirlik Muafiyet Sinavlari", StartDate = Utc(2025, 9, 9), EndDate = Utc(2025, 9, 12), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_prep_exam" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.CourseExemptionResult, Title = "Ders Muafiyet Sonuclarinin Ilani", StartDate = Utc(2025, 9, 12), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_exemption_result" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.GraduateResult, Title = "Lisansustu Program Basvuru Sonuclarinin Ilani", StartDate = Utc(2025, 9, 12), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_graduate_result" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.Orientation, Title = "Yeni Kayit Olan Ogrenciler icin Oryantasyon", StartDate = Utc(2025, 9, 15), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_orientation" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.CourseRegistration, Title = "Ders Kayit ve Danisman Onaylari", StartDate = Utc(2025, 9, 15), EndDate = Utc(2025, 9, 19), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_course_registration" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.GraduateRegistration, Title = "Lisansustu Ucret Odeme, Ders Kayitlari ve Danisman Onaylari", StartDate = Utc(2025, 9, 15), EndDate = Utc(2025, 9, 19), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_graduate_registration" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.ClassesStart, Title = "Derslerin Baslamasi", StartDate = Utc(2025, 9, 22), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "classes_start" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.AddDrop, Title = "Ders Ekleme - Birakma", StartDate = Utc(2025, 9, 29), EndDate = Utc(2025, 10, 3), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "add_drop" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.ScholarshipApplication, Title = "Burs Basvurularinin Alinmasi", StartDate = Utc(2025, 9, 29), EndDate = Utc(2025, 10, 10), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_scholarship_app" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.NationalHoliday, Title = "Cumhuriyet Bayrami", StartDate = Utc(2025, 10, 28), EndDate = Utc(2025, 10, 29), IsPublic = true, IsGenerated = true, ChainCode = "HOLIDAY", BlocksAcademicEvents = true },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.CourseWithdrawal, Title = "Dersten Cekilme Basvurularinin Son Gunu", StartDate = Utc(2025, 11, 7), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "withdrawal_deadline" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.RegistrationFreezeDeadline, Title = "Kayit Dondurma Basvurularinin Son Gunu", StartDate = Utc(2025, 11, 7), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "freeze_deadline" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.ScholarshipResult, Title = "Burs Basvuru Sonuclarinin Aciklanmasi", StartDate = Utc(2025, 11, 7), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_scholarship_result" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.ImportantDay, Title = "Ataturk'u Anma Gunu", StartDate = Utc(2025, 11, 10), IsPublic = true, IsGenerated = true, ChainCode = "HOLIDAY", BlocksAcademicEvents = true },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.MidtermExam, Title = "Ara Sinavlar", StartDate = Utc(2025, 11, 10), EndDate = Utc(2025, 11, 15), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "midterm_exam" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.ExcuseExamApplication, Title = "Mazeret Sinav Basvurularinin Son Gunu", StartDate = Utc(2025, 11, 17), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "midterm_excuse_application" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.ExcuseExam, Title = "Mazeret Sinavlari", StartDate = Utc(2025, 11, 19), EndDate = Utc(2025, 11, 21), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "midterm_excuse_exam" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.MidtermGradePublication, Title = "Ara Sinavlarin Ilaninin Son Gunu", StartDate = Utc(2025, 11, 28), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "midterm_publication" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.ScholarshipRefund, Title = "Burs Iadelerinin Yapilmasi", StartDate = Utc(2025, 11, 28), IsPublic = true, IsGenerated = true, ChainCode = "ADMISSIONS", TemplateItemKey = "fall_scholarship_refund" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.NationalHoliday, Title = "Yilbasi Tatili", StartDate = Utc(2026, 1, 1), IsPublic = true, IsGenerated = true, ChainCode = "HOLIDAY", BlocksAcademicEvents = true },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.ClassesEnd, Title = "Derslerin Sona Ermesi", StartDate = Utc(2026, 1, 2), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "classes_end" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.FinalExam, Title = "Donem Sonu Sinavlari", StartDate = Utc(2026, 1, 5), EndDate = Utc(2026, 1, 16), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "final_exam" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.PrepSchoolExam, Title = "Hazirlik Muafiyet Sinavlari", StartDate = Utc(2026, 1, 12), EndDate = Utc(2026, 1, 15), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "prep_final_exam" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.FinalGradePublication, Title = "Donem Sonu Sinav Sonuclarinin Ilani", StartDate = Utc(2026, 1, 21), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "final_publication" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.MakeupExam, Title = "Butunleme Sinavlari Basvuru Son Gunu", StartDate = Utc(2026, 1, 23), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "makeup_application" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.MakeupExam, Title = "Butunleme Sinavlari", StartDate = Utc(2026, 1, 26), EndDate = Utc(2026, 1, 31), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "makeup_exam" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.MakeupGradePublication, Title = "Butunleme Sinavlari Sonuclarinin Ilaninin Son Gunu", StartDate = Utc(2026, 2, 6), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "makeup_publication" },
                new TermCalendarEvent { TermId = fallTerm.Id, EventType = CalendarEventType.SemesterBreak, Title = "Yariyil Tatili", StartDate = Utc(2026, 1, 30), EndDate = Utc(2026, 2, 20), IsPublic = true, IsGenerated = true, ChainCode = "MAIN", TemplateItemKey = "fall_semester_break" });
        }

        if (summerTerm is not null)
        {
            context.TermCalendarEvents.AddRange(
                new TermCalendarEvent { TermId = summerTerm.Id, EventType = CalendarEventType.SummerSchoolRegistration, Title = "Yaz Ogretimi Ucret Odemeleri ve Ders Kayitlari", StartDate = Utc(2026, 6, 30), EndDate = Utc(2026, 7, 3), IsPublic = true, IsGenerated = true, ChainCode = "SUMMER", TemplateItemKey = "summer_registration" },
                new TermCalendarEvent { TermId = summerTerm.Id, EventType = CalendarEventType.ClassesStart, Title = "Yaz Ogretimi Derslerin Baslamasi", StartDate = Utc(2026, 7, 13), IsPublic = true, IsGenerated = true, ChainCode = "SUMMER", TemplateItemKey = "summer_classes_start" },
                new TermCalendarEvent { TermId = summerTerm.Id, EventType = CalendarEventType.NationalHoliday, Title = "Demokrasi ve Milli Birlik Gunu", StartDate = Utc(2026, 7, 15), IsPublic = true, IsGenerated = true, ChainCode = "HOLIDAY", BlocksAcademicEvents = true },
                new TermCalendarEvent { TermId = summerTerm.Id, EventType = CalendarEventType.SummerSchoolExam, Title = "Yaz Ogretimi Sinavlari", StartDate = Utc(2026, 8, 24), EndDate = Utc(2026, 8, 28), IsPublic = true, IsGenerated = true, ChainCode = "SUMMER", TemplateItemKey = "summer_exam" },
                new TermCalendarEvent { TermId = summerTerm.Id, EventType = CalendarEventType.ClassesEnd, Title = "Yaz Ogretimi Derslerin Sona Ermesi", StartDate = Utc(2026, 8, 28), IsPublic = true, IsGenerated = true, ChainCode = "SUMMER", TemplateItemKey = "summer_classes_end" },
                new TermCalendarEvent { TermId = summerTerm.Id, EventType = CalendarEventType.ExcuseExamApplication, Title = "Mazeret Sinav Basvurularinin Son Gunu", StartDate = Utc(2026, 8, 31), IsPublic = true, IsGenerated = true, ChainCode = "SUMMER", TemplateItemKey = "summer_excuse_application" },
                new TermCalendarEvent { TermId = summerTerm.Id, EventType = CalendarEventType.ExcuseExam, Title = "Mazeret Sinavlari", StartDate = Utc(2026, 9, 2), IsPublic = true, IsGenerated = true, ChainCode = "SUMMER", TemplateItemKey = "summer_excuse_exam" },
                new TermCalendarEvent { TermId = summerTerm.Id, EventType = CalendarEventType.SummerSchoolGradePublication, Title = "Yaz Ogretimi Sinav Sonuclarinin Ilaninin Son Gunu", StartDate = Utc(2026, 9, 4), IsPublic = true, IsGenerated = true, ChainCode = "SUMMER", TemplateItemKey = "summer_publication" });
        }
        await context.SaveChangesAsync();
    }

    private static DateTime ParseUtcDate(string value) =>
        DateTime.SpecifyKind(
            DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeKind.Utc);

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
