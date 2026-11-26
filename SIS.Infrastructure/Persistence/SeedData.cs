using Microsoft.AspNetCore.Identity;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence.Seeds;
using SIS.Infrastructure.Services.CalendarGenerator;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedAsync(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        await SeedSystemAsync(context, roleManager);
        await SeedDemoAsync(context, userManager);
    }

    // Her kurulumda gereken sistem verisi. Örnek veriye bağlı değildir.
    public static async Task SeedSystemAsync(
        AppDbContext context,
        RoleManager<IdentityRole<int>> roleManager)
    {
        // Roller ve izinler sistem kataloğundan (anahtarla, yalnız eksik olanı ekler).
        await Katalog.SistemKatalogu.EsitleAsync(context, DateTime.UtcNow);
        await new CalendarTemplateService(context).EnsureDefaultsAsync();
        // SystemConfig, zaman dilimlerinden önce yazılmalı: zaman dilimi üretimi Schedule.* anahtarlarını okuyor.
        await SeedSystemConfigAsync(context);
        await SeedWebModuleSettingsAsync(context);
        await SeedAnnouncementCategoriesAsync(context);
        await SeedConstraintDefinitionsAsync(context);
        await SeedEvaluationQuestionsAsync(context);

        // ── Course lookups (Açma Nedeni + Ders Kategorisi) ────
        await CurriculumCourseOpeningReasonSeed.SeedAsync(context);
        await CourseCategorySeed.SeedAsync(context);
        await ElectiveSubtypeSeed.SeedAsync(context);
        await DocumentTypeSeed.SeedAsync(context);

        // ── RBAC Phase 1 (Sprint 1.4) ──────────────────────────
        // Sıra önemli: Permission (katalog, yukarıda) → MenuStructure (perm lookup gerekli) → RolePermission (perm + rol lookup)
        await Katalog.MenuKatalogu.EsitleAsync(context, DateTime.UtcNow);
        await RolePermissionSeed.SeedAsync(context, roleManager);

        // ── RBAC Phase 2 (Permission Bundles) ───────────────────
        // MenuStructureSeed sonrası: bundle'lar menu modüllerinden derive ediliyor
        await PermissionBundleSeed.SeedAsync(context);

        // ── Rapor Motoru (Faz 1): handler'ı olan raporları idempotent ekle ──
        await ReportSeed.SeedAsync(context);
    }

    // Örnek (demo) veri: akademik yapı, kullanıcılar, dönemler, kayıtlar. Sistem verisinden sonra çalışır.
    public static async Task SeedDemoAsync(
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        await SeedAcademicStructureAsync(context);
        await SeedCoursesAsync(context);
        await SeedTermsAndClassroomsAsync(context);
        await SeedUsersAsync(context, userManager);
        await SeedRegistrationsAsync(context);    // Geçmiş dönem transkript verileri (aktif döneme dokunmaz)
        // SeedSectionSchedulesAsync devre dışı — schedule'lar scheduling sistemi tarafından oluşturulmalı
        await SeedTimeSlotsAsync(context);
    }
}
