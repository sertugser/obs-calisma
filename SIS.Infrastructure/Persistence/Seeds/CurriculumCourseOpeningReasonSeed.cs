using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds the 21 standard "Açma Nedeni" values from Eski Proliz.
/// Katalog kuralı: Code'su tabloda da katalog kaydında da olmayanı ekler; mevcut kayıtlara dokunmaz, yöneticinin sildiği geri gelmez.
/// "Dönemsel (Teorik)" carries the special bypass flags that match Proliz behavior.
/// </summary>
public static class CurriculumCourseOpeningReasonSeed
{
    public static readonly IReadOnlyList<CurriculumCourseOpeningReason> All = new List<CurriculumCourseOpeningReason>
    {
        New("Periodic", "Dönemsel", null, 10),
        New("Erasmus", "Erasmus", null, 20),
        New("OutOfField", "Alan Dışı", null, 30),
        // Faz 4 — Staj GPA dışı (akademik standart)
        New("Internship", "Staj", null, 40, isExcludedFromGpa: true),
        New("LangSupport", "Y.Dil Destek", null, 50),
        // Faz 4 — Hobi dersleri GPA dışı (öğrencinin not ortalamasını etkilemez)
        New("Hobby", "Hobi", null, 60, isExcludedFromGpa: true),
        New("DistanceEducation", "Uzaktan Eğitim", null, 70),
        New(
            "PeriodicTheory",
            "Dönemsel (Teorik)",
            "Alttan uyg. ders devam zorunluluğu çakışma kontrolü bu ders için geçersiz",
            80,
            bypassScheduleConflict: true,
            bypassMandatoryAttendance: true),
        New("Preparatory", "Hazırlık", null, 90),
        // Faz 4 — Tez/Uzmanlık Alan/Seminer/Danışmanlık dersleri GPA dışı (lisansüstü standart)
        New("Thesis", "Tez", null, 100, isExcludedFromGpa: true),
        New("ExpertiseField", "Uzmanlık Alan", null, 110, isExcludedFromGpa: true),
        New("Seminar", "Seminer", null, 120, isExcludedFromGpa: true),
        New("Counseling", "Danışmanlık", null, 130, isExcludedFromGpa: true),
        New("MidtermBarrier", "Vize Barajlı", null, 140),
        New("MedServiceCourses", "Tıp Servis Dersleri", null, 150),
        New("MedCommittee", "Tıp Kurul", null, 160),
        New("MedInternship", "Tıp Staj", null, 170),
        New("MedInternIntern", "Tıp Staj(İntörn)", null, 180),
        New("MedCommitteeBarrier", "Tıp Kurul Vize Barajlı", null, 190),
        New("DentalApplication", "Diş Uygulama", null, 200),
        New("Other", "Diğer", null, 210),
    };

    public static async Task SeedAsync(AppDbContext context)
    {
        // Katalog kuralı: eksik açma nedeni eklenir, yöneticinin sildiği geri gelmez, var olana dokunulmaz.
        await Katalog.KatalogEsitleyici.EsitleAsync(context, new Katalog.KatalogTuru<CurriculumCourseOpeningReason, CurriculumCourseOpeningReason>
        {
            Ad = "acma-nedeni",
            Kayitlar = All,
            KayitAnahtari = r => r.Code,
            VarlikAnahtari = r => r.Code,
            Olustur = (r, simdi) => new CurriculumCourseOpeningReason
            {
                Code = r.Code, Name = r.Name, Description = r.Description, IsActive = r.IsActive, SortOrder = r.SortOrder,
                AllowsAdvisorGrading = r.AllowsAdvisorGrading, BypassScheduleConflict = r.BypassScheduleConflict,
                BypassMandatoryAttendance = r.BypassMandatoryAttendance, IsExcludedFromGpa = r.IsExcludedFromGpa,
                IsCreditFree = r.IsCreditFree, RequiresApproval = r.RequiresApproval,
                AllowsCustomGradingScale = r.AllowsCustomGradingScale, CreatedAt = simdi,
            },
        }, DateTime.UtcNow);
    }

    private static CurriculumCourseOpeningReason New(
        string code, string name, string? description, int sortOrder,
        bool allowsAdvisorGrading = false,
        bool bypassScheduleConflict = false,
        bool bypassMandatoryAttendance = false,
        bool isExcludedFromGpa = false,
        bool isCreditFree = false,
        bool requiresApproval = false,
        bool allowsCustomGradingScale = false) => new()
    {
        Code = code,
        Name = name,
        Description = description,
        SortOrder = sortOrder,
        IsActive = true,
        AllowsAdvisorGrading = allowsAdvisorGrading,
        BypassScheduleConflict = bypassScheduleConflict,
        BypassMandatoryAttendance = bypassMandatoryAttendance,
        IsExcludedFromGpa = isExcludedFromGpa,
        IsCreditFree = isCreditFree,
        RequiresApproval = requiresApproval,
        AllowsCustomGradingScale = allowsCustomGradingScale,
    };
}
