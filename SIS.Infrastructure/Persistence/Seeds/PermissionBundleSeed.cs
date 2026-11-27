using Microsoft.EntityFrameworkCore;
using SIS.Domain.Identity;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// RBAC Phase 2 — Sidebar-grubu bazlı default permission bundle'ları seed eder.
///
/// Her top-level <see cref="MenuModule"/> için iki bundle oluşturulur:
///   1. "<ModuleLabel> — Görüntüleme"  →  sadece .view permission'ları
///   2. "<ModuleLabel> — Tam Yetki"    →  modüldeki tüm permission'lar
///
/// Idempotent: aynı isimde bundle varsa atlanır. <see cref="PermissionBundle.IsSystemDefault"/>=true
/// olduğu için silinmesi engellenir; içerik admin tarafından düzenlenebilir (Karar 4 dışında snapshot semantik).
///
/// Çalışma sırası: sistem kataloğu (izinler) → menü kataloğu → bu seed.
/// </summary>
public static class PermissionBundleSeed
{
    private const string ViewSuffix = " — Görüntüleme";
    private const string FullSuffix = " — Tam Yetki";

    public static async Task SeedAsync(AppDbContext context, CancellationToken ct = default)
    {
        // Top-level modüller (ParentId = null) — alt modüller dışlanır
        var topModules = await context.MenuModules
            .AsNoTracking()
            .Where(m => m.ParentId == null)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);

        if (topModules.Count == 0) return;

        // Her modül için: o modül + alt modülleri altındaki MenuItem'lara bağlı tüm permission'ları topla
        var allModulesById = await context.MenuModules
            .AsNoTracking()
            .ToDictionaryAsync(m => m.Id, ct);

        var allMenuItemPermissions = await context.MenuItemPermissions
            .AsNoTracking()
            .Include(mip => mip.MenuItem)
            .Include(mip => mip.Permission)
            .ToListAsync(ct);

        var existingBundleNames = await context.PermissionBundles
            .Select(b => b.Name)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingBundleNames, StringComparer.Ordinal);

        var newBundles = new List<PermissionBundle>();
        var now = DateTime.UtcNow;

        foreach (var module in topModules)
        {
            // Bu modül + alt modüllerinin ID'leri
            var descendantIds = CollectDescendantIds(module.Id, allModulesById);

            // Bu modüllere bağlı menu item'ların permission'ları
            var permissionsInModule = allMenuItemPermissions
                .Where(mip => mip.MenuItem != null && descendantIds.Contains(mip.MenuItem.ModuleId))
                .Select(mip => mip.Permission!)
                .Where(p => p != null)
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToList();

            if (permissionsInModule.Count == 0) continue;

            var viewBundleName = module.Label + ViewSuffix;
            var fullBundleName = module.Label + FullSuffix;

            if (!existingSet.Contains(viewBundleName))
            {
                var viewPerms = permissionsInModule
                    .Where(p => p.Key.EndsWith(".view", StringComparison.Ordinal))
                    .Select(p => p.Id)
                    .Distinct()
                    .ToList();
                if (viewPerms.Count > 0)
                {
                    newBundles.Add(new PermissionBundle
                    {
                        Name = viewBundleName,
                        Description = $"{module.Label} grubundaki tüm sayfaların görüntüleme yetkileri.",
                        Category = module.Label,
                        IsSystemDefault = true,
                        CreatedAt = now,
                        UpdatedAt = now,
                        Items = viewPerms.Select(pid => new PermissionBundleItem { PermissionId = pid }).ToList(),
                    });
                }
            }

            if (!existingSet.Contains(fullBundleName))
            {
                var allPerms = permissionsInModule.Select(p => p.Id).Distinct().ToList();
                newBundles.Add(new PermissionBundle
                {
                    Name = fullBundleName,
                    Description = $"{module.Label} grubundaki tüm sayfaların tüm yetkileri (görüntüleme + işlem).",
                    Category = module.Label,
                    IsSystemDefault = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Items = allPerms.Select(pid => new PermissionBundleItem { PermissionId = pid }).ToList(),
                });
            }
        }

        if (newBundles.Count == 0) return;

        context.PermissionBundles.AddRange(newBundles);
        await context.SaveChangesAsync(ct);
    }

    private static HashSet<int> CollectDescendantIds(int rootId, IReadOnlyDictionary<int, MenuModule> all)
    {
        var ids = new HashSet<int> { rootId };
        var queue = new Queue<int>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var (id, module) in all)
            {
                if (module.ParentId == current && ids.Add(id))
                {
                    queue.Enqueue(id);
                }
            }
        }
        return ids;
    }
}
