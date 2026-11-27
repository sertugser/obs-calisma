using Microsoft.EntityFrameworkCore;
using SIS.Application.Identity.DTOs;
using SIS.Application.Identity.Services;
using SIS.Domain.Identity;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class MenuAdminService : IMenuAdminService
{
    private readonly AppDbContext _context;

    public MenuAdminService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<MenuModuleAdminDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var modules = await _context.MenuModules
            .AsNoTracking()
            .AsSplitQuery()
            .Include(m => m.Items)
                .ThenInclude(i => i.MenuItemPermissions)
                    .ThenInclude(mip => mip.Permission)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);

        return modules
            .Where(m => m.ParentId == null)
            .OrderBy(m => m.SortOrder)
            .Select(m => MapModule(m, modules))
            .ToList();
    }

    private static MenuModuleAdminDto MapModule(MenuModule module, List<MenuModule> all)
    {
        return new MenuModuleAdminDto
        {
            Id = module.Id,
            ParentId = module.ParentId,
            Slug = module.Slug,
            Label = module.Label,
            Icon = module.Icon,
            SortOrder = module.SortOrder,
            IsActive = module.IsActive,
            AllowedRolesCsv = module.AllowedRolesCsv,
            Items = module.Items
                .OrderBy(i => i.SortOrder)
                .Select(MapItem)
                .ToList(),
            Children = all
                .Where(c => c.ParentId == module.Id)
                .OrderBy(c => c.SortOrder)
                .Select(c => MapModule(c, all))
                .ToList(),
        };
    }

    private static MenuItemAdminDto MapItem(MenuItem item) => new()
    {
        Id = item.Id,
        ModuleId = item.ModuleId,
        Label = item.Label,
        Route = item.Route,
        Slug = item.Slug,
        SortOrder = item.SortOrder,
        OpenInNewTab = item.OpenInNewTab,
        IsActive = item.IsActive,
        PermissionKeys = item.MenuItemPermissions
            .Where(mip => mip.Permission != null)
            .Select(mip => mip.Permission!.Key)
            .OrderBy(k => k)
            .ToList(),
    };

    public async Task<MenuModuleAdminDto> CreateModuleAsync(CreateMenuModuleDto dto, CancellationToken ct = default)
    {
        ValidateSlug(dto.Slug);
        if (string.IsNullOrWhiteSpace(dto.Label))
            throw new ArgumentException("Label zorunludur.");

        if (await _context.MenuModules.AnyAsync(m => m.Slug == dto.Slug, ct))
            throw new InvalidOperationException($"'{dto.Slug}' slug'ı zaten kullanılıyor.");

        if (dto.ParentId.HasValue)
        {
            var parentExists = await _context.MenuModules.AnyAsync(m => m.Id == dto.ParentId.Value, ct);
            if (!parentExists) throw new ArgumentException("Parent modül bulunamadı.");
        }

        var allowedRolesCsv = await NormalizeAndValidateRolesCsvAsync(dto.AllowedRolesCsv, ct);
        var nextSort = await NextModuleSortOrder(dto.ParentId, ct);

        var module = new MenuModule
        {
            ParentId = dto.ParentId,
            Slug = dto.Slug.Trim(),
            Label = dto.Label.Trim(),
            Icon = string.IsNullOrWhiteSpace(dto.Icon) ? null : dto.Icon.Trim(),
            AllowedRolesCsv = allowedRolesCsv,
            SortOrder = nextSort,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.MenuModules.Add(module);
        await _context.SaveChangesAsync(ct);

        return new MenuModuleAdminDto
        {
            Id = module.Id,
            ParentId = module.ParentId,
            Slug = module.Slug,
            Label = module.Label,
            Icon = module.Icon,
            SortOrder = module.SortOrder,
            IsActive = module.IsActive,
            AllowedRolesCsv = module.AllowedRolesCsv,
        };
    }

    public async Task<MenuModuleAdminDto> UpdateModuleAsync(int moduleId, UpdateMenuModuleDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Label))
            throw new ArgumentException("Label zorunludur.");

        var module = await _context.MenuModules.FirstOrDefaultAsync(m => m.Id == moduleId, ct)
            ?? throw new KeyNotFoundException("Modül bulunamadı.");

        var allowedRolesCsv = await NormalizeAndValidateRolesCsvAsync(dto.AllowedRolesCsv, ct);

        module.Label = dto.Label.Trim();
        module.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? null : dto.Icon.Trim();
        module.AllowedRolesCsv = allowedRolesCsv;
        module.IsActive = dto.IsActive;
        module.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return new MenuModuleAdminDto
        {
            Id = module.Id,
            ParentId = module.ParentId,
            Slug = module.Slug,
            Label = module.Label,
            Icon = module.Icon,
            SortOrder = module.SortOrder,
            IsActive = module.IsActive,
            AllowedRolesCsv = module.AllowedRolesCsv,
        };
    }

    public async Task DeleteModuleAsync(int moduleId, CancellationToken ct = default)
    {
        var module = await _context.MenuModules.FirstOrDefaultAsync(m => m.Id == moduleId, ct)
            ?? throw new KeyNotFoundException("Modül bulunamadı.");
        _context.MenuModules.Remove(module);
        await _context.SaveChangesAsync(ct);
    }

    public async Task ReorderModulesAsync(ReorderModulesDto dto, CancellationToken ct = default)
    {
        if (dto.Entries.Count == 0) return;

        var ids = dto.Entries.Select(e => e.Id).ToList();
        var modules = await _context.MenuModules
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(ct);

        if (modules.Count != ids.Count)
            throw new ArgumentException("Bir veya daha fazla modül bulunamadı.");

        if (dto.ParentId.HasValue)
        {
            var parentExists = await _context.MenuModules.AnyAsync(m => m.Id == dto.ParentId.Value, ct);
            if (!parentExists) throw new ArgumentException("Parent modül bulunamadı.");
            // Self / cycle koruma: hiçbir taşınan modül yeni parent'ın atası olamaz
            foreach (var m in modules)
            {
                if (m.Id == dto.ParentId.Value)
                    throw new InvalidOperationException("Modül kendi altına taşınamaz.");
                if (await IsAncestor(m.Id, dto.ParentId.Value, ct))
                    throw new InvalidOperationException("Modül kendi alt-ağacına taşınamaz (cycle).");
            }
        }

        var byId = dto.Entries.ToDictionary(e => e.Id, e => e.SortOrder);
        foreach (var m in modules)
        {
            m.ParentId = dto.ParentId;
            m.SortOrder = byId[m.Id];
            m.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(ct);
    }

    private async Task<bool> IsAncestor(int candidateAncestorId, int descendantId, CancellationToken ct)
    {
        // candidateAncestorId, descendantId'nin atası mı?
        var current = await _context.MenuModules.AsNoTracking()
            .Where(m => m.Id == descendantId)
            .Select(m => new { m.Id, m.ParentId })
            .FirstOrDefaultAsync(ct);
        while (current?.ParentId != null)
        {
            if (current.ParentId == candidateAncestorId) return true;
            var pid = current.ParentId.Value;
            current = await _context.MenuModules.AsNoTracking()
                .Where(m => m.Id == pid)
                .Select(m => new { m.Id, m.ParentId })
                .FirstOrDefaultAsync(ct);
        }
        return false;
    }

    public async Task<MenuItemAdminDto> CreateItemAsync(CreateMenuItemDto dto, CancellationToken ct = default)
    {
        ValidateSlug(dto.Slug);
        if (string.IsNullOrWhiteSpace(dto.Label)) throw new ArgumentException("Label zorunludur.");
        ValidateRoute(dto.Route);

        var module = await _context.MenuModules.FirstOrDefaultAsync(m => m.Id == dto.ModuleId, ct)
            ?? throw new ArgumentException("Modül bulunamadı.");

        if (await _context.MenuItems.AnyAsync(mi => mi.ModuleId == dto.ModuleId && mi.Route == dto.Route, ct))
            throw new InvalidOperationException("Bu modülde aynı route'a sahip başka bir item var.");

        // Öğe slug'ı veritabanında tekil (menü kataloğu öğeyi slug'la tanır).
        if (await _context.MenuItems.AnyAsync(mi => mi.Slug == dto.Slug, ct))
            throw new InvalidOperationException($"'{dto.Slug}' slug'ı zaten kullanılıyor.");

        var permissions = await ResolvePermissionsAsync(dto.PermissionKeys, ct);
        EnsureHasPermission(permissions);

        var nextSort = await NextItemSortOrder(dto.ModuleId, ct);
        var item = new MenuItem
        {
            ModuleId = dto.ModuleId,
            Label = dto.Label.Trim(),
            Route = dto.Route.Trim(),
            Slug = dto.Slug.Trim(),
            SortOrder = nextSort,
            OpenInNewTab = dto.OpenInNewTab,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.MenuItems.Add(item);
        await _context.SaveChangesAsync(ct);

        foreach (var p in permissions)
        {
            _context.MenuItemPermissions.Add(new MenuItemPermission { MenuItemId = item.Id, PermissionId = p.Id });
        }
        await _context.SaveChangesAsync(ct);

        return new MenuItemAdminDto
        {
            Id = item.Id,
            ModuleId = item.ModuleId,
            Label = item.Label,
            Route = item.Route,
            Slug = item.Slug,
            SortOrder = item.SortOrder,
            OpenInNewTab = item.OpenInNewTab,
            IsActive = item.IsActive,
            PermissionKeys = permissions.Select(p => p.Key).OrderBy(k => k).ToList(),
        };
    }

    public async Task<MenuItemAdminDto> UpdateItemAsync(int itemId, UpdateMenuItemDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Label)) throw new ArgumentException("Label zorunludur.");
        ValidateRoute(dto.Route);

        var item = await _context.MenuItems
            .Include(i => i.MenuItemPermissions)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new KeyNotFoundException("Menü item bulunamadı.");

        if (item.Route != dto.Route)
        {
            var clash = await _context.MenuItems.AnyAsync(
                mi => mi.ModuleId == item.ModuleId && mi.Route == dto.Route && mi.Id != itemId, ct);
            if (clash) throw new InvalidOperationException("Bu modülde aynı route'a sahip başka bir item var.");
        }

        var permissions = await ResolvePermissionsAsync(dto.PermissionKeys, ct);
        EnsureHasPermission(permissions);

        item.Label = dto.Label.Trim();
        item.Route = dto.Route.Trim();
        item.OpenInNewTab = dto.OpenInNewTab;
        item.IsActive = dto.IsActive;
        item.UpdatedAt = DateTime.UtcNow;

        var newIds = permissions.Select(p => p.Id).ToHashSet();
        var existing = item.MenuItemPermissions.ToList();

        foreach (var mip in existing.Where(mip => !newIds.Contains(mip.PermissionId)))
            _context.MenuItemPermissions.Remove(mip);

        var existingIds = existing.Select(mip => mip.PermissionId).ToHashSet();
        foreach (var p in permissions.Where(p => !existingIds.Contains(p.Id)))
            _context.MenuItemPermissions.Add(new MenuItemPermission { MenuItemId = item.Id, PermissionId = p.Id });

        await _context.SaveChangesAsync(ct);

        return new MenuItemAdminDto
        {
            Id = item.Id,
            ModuleId = item.ModuleId,
            Label = item.Label,
            Route = item.Route,
            Slug = item.Slug,
            SortOrder = item.SortOrder,
            OpenInNewTab = item.OpenInNewTab,
            IsActive = item.IsActive,
            PermissionKeys = permissions.Select(p => p.Key).OrderBy(k => k).ToList(),
        };
    }

    public async Task DeleteItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await _context.MenuItems.FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new KeyNotFoundException("Menü item bulunamadı.");
        _context.MenuItems.Remove(item);
        await _context.SaveChangesAsync(ct);
    }

    public async Task ReorderItemsAsync(ReorderItemsDto dto, CancellationToken ct = default)
    {
        if (dto.Entries.Count == 0) return;

        var ids = dto.Entries.Select(e => e.Id).ToList();
        var items = await _context.MenuItems
            .Where(i => ids.Contains(i.Id))
            .ToListAsync(ct);

        if (items.Count != ids.Count)
            throw new ArgumentException("Bir veya daha fazla item bulunamadı.");

        // Tüm item'lar aynı modüle ait olmalı; cross-module taşıma bu endpoint'in scope'unda değil
        if (items.Any(i => i.ModuleId != dto.ModuleId))
        {
            // İzin ver: caller item'ı yeni modüle taşımak istiyor → ModuleId'yi güncelle
            // (Route uniqueness yeni modülde de geçerli olmalı)
            foreach (var i in items.Where(i => i.ModuleId != dto.ModuleId))
            {
                var clash = await _context.MenuItems.AnyAsync(
                    mi => mi.ModuleId == dto.ModuleId && mi.Route == i.Route && mi.Id != i.Id, ct);
                if (clash) throw new InvalidOperationException(
                    $"Hedef modülde '{i.Route}' route'una sahip başka item var (Item #{i.Id}).");
                i.ModuleId = dto.ModuleId;
            }
        }

        var byId = dto.Entries.ToDictionary(e => e.Id, e => e.SortOrder);
        foreach (var i in items)
        {
            i.SortOrder = byId[i.Id];
            i.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(ct);
    }

    private async Task<int> NextModuleSortOrder(int? parentId, CancellationToken ct)
    {
        var max = await _context.MenuModules
            .Where(m => m.ParentId == parentId)
            .Select(m => (int?)m.SortOrder)
            .MaxAsync(ct);
        return (max ?? 0) + 1;
    }

    private async Task<int> NextItemSortOrder(int moduleId, CancellationToken ct)
    {
        var max = await _context.MenuItems
            .Where(i => i.ModuleId == moduleId)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync(ct);
        return (max ?? 0) + 1;
    }

    private async Task<List<Permission>> ResolvePermissionsAsync(List<string> keys, CancellationToken ct)
    {
        if (keys is null || keys.Count == 0) return new();
        var distinct = keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct().ToList();
        var found = await _context.Permissions.Where(p => distinct.Contains(p.Key)).ToListAsync(ct);
        if (found.Count != distinct.Count)
        {
            var missing = distinct.Except(found.Select(p => p.Key)).ToList();
            throw new ArgumentException($"Bilinmeyen permission key: {string.Join(", ", missing)}");
        }
        return found;
    }

    private static void ValidateSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug zorunludur.");
        // basit kontrol: a-z 0-9 -, başka karakter yok
        foreach (var ch in slug)
        {
            if (!(ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
                throw new ArgumentException("Slug sadece küçük harf, rakam ve tire içerebilir.");
        }
    }

    private static void ValidateRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            throw new ArgumentException("Route zorunludur.");
        if (!route.Trim().StartsWith('/'))
            throw new ArgumentException("Route '/' ile başlamalıdır.");
    }

    private static void EnsureHasPermission(List<Permission> permissions)
    {
        // Yetkisiz item sidebar filtresinde (MenuService) hiç kimseye görünmez —
        // sessiz kayıp yerine erken hata.
        if (permissions.Count == 0)
            throw new ArgumentException("Sayfa için en az bir yetki seçilmelidir.");
    }

    /// <summary>
    /// CSV'yi normalize eder ve her rol adının gerçekten var olduğunu doğrular.
    /// Tanımsız rol → modül sessizce kimseye görünmez; bu yüzden erken hata veriyoruz.
    /// </summary>
    private async Task<string?> NormalizeAndValidateRolesCsvAsync(string? csv, CancellationToken ct)
    {
        var normalized = NormalizeCsv(csv);
        if (normalized is null) return null;

        var roles = normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var knownRoles = await _context.Roles
            .Select(r => r.Name!)
            .ToListAsync(ct);
        var unknown = roles.Where(r => !knownRoles.Contains(r, StringComparer.Ordinal)).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"Bilinmeyen rol: {string.Join(", ", unknown)}");

        return normalized;
    }

    private static string? NormalizeCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : string.Join(",", parts);
    }
}
