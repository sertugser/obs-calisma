# Permission System (RBAC Phase 1) — Codemap

> **Phase 1 MVP** — DB-driven role/permission/menu sistemi. SystemAdmin admin UI'dan yetki değişikliği yapabilir, deployment gerekmez.
>
> **Plan referans:** `docs/superpowers/plans/2026-04-25-rbac-dynamic-menu-phase1.md` (gitignore'da, local).
> **Tag:** `rbac-phase1-mvp`

## Hızlı Tanıtım

| Soru | Cevap |
|------|-------|
| Yetki kontrolü nerede yapılır? | Backend `[RequirePermission("...")]` attribute (controller method-level), frontend `usePermissions().hasPermission(key)` |
| SystemAdmin nasıl her şeye erişir? | Kod-shortcut: `RequirePermissionAttribute` + `PermissionService.GetUserPermissionsAsync` `IsInRole("SystemAdministrator")` ise erken dönüş. DB'de RolePermission satırı YOK. |
| Permission key formatı | `<category>.<resource>.<action>` (örn. `menu.akademik-donem.cancel`) — 3 kategori: `menu.*`, `lookup.*`, `system.*` |
| Permission değişikliği ne kadar sürede yansır? | Backend cache 5 dk TTL → ilgili rolün tüm kullanıcıları max 5 dk içinde refresh'te güncel görür |
| Menü nereden geliyor? | DB'den dinamik (`MenuModule` + `MenuItem`); frontend `useMenu()` hook ile çeker, permission'a göre filtrelenmiş liste döner |

## Permission Key Naming

```
<category>.<resource>.<action>
```

### 3 Kategori

| Kategori | Örnek | DB seed | Açıklama |
|----------|-------|---------|----------|
| `menu.*` | `menu.akademik-donem.view`, `menu.fakulte-tanimlari.edit` | ✅ Var (~237 key) | Sidebar item'lara bağlı endpoint'ler |
| `lookup.*` | `lookup.faculty`, `lookup.term` | ❌ Yok (Karar 7) | Dropdown lookup'lar — `RequirePermissionAttribute` içinde hardcoded shortcut: her authenticated user'a açık |
| `system.*` | `system.permission.view`, `system.permission.assign` | ✅ Var (~2 key) | Admin UI operasyonları, sadece SysAdmin için |

### Action suffix konvansiyonu

| Action | Anlam |
|--------|-------|
| `.view` | Görüntüle/listele/oku |
| `.create` | Yeni oluştur |
| `.edit` | Düzenle/güncelle |
| `.delete` | Sil |
| `.{verb}` | Domain-specific (`.cancel`, `.approve`, `.lock-grades`, `.promote`, vb.) |

## DB Schema (5 tablo)

[`SIS.Domain/Identity/`](../../SIS.Domain/Identity/) altında 5 entity:

| Entity | Dosya | Açıklama |
|--------|-------|----------|
| `Permission` | [Permission.cs](../../SIS.Domain/Identity/Permission.cs) | (Id, Key UNIQUE, Category, Description) |
| `RolePermission` | [RolePermission.cs](../../SIS.Domain/Identity/RolePermission.cs) | Composite PK (RoleId int, PermissionId int) |
| `MenuModule` | [MenuModule.cs](../../SIS.Domain/Identity/MenuModule.cs) | Self-ref tree (ParentId) |
| `MenuItem` | [MenuItem.cs](../../SIS.Domain/Identity/MenuItem.cs) | Route UNIQUE, ModuleId FK |
| `MenuItemPermission` | [MenuItemPermission.cs](../../SIS.Domain/Identity/MenuItemPermission.cs) | M:N item↔permission |

Migration: [`20260425132603_AddRBACPermissions`](../../SIS.Infrastructure/Migrations/20260425132603_AddRBACPermissions.cs) — 5 tablo + 8 index + 6 FK + cascade.

**ER (özet):**
```
AspNetRoles  ──< RolePermissions >──  Permissions
                                          ▲
                                          │
                            MenuItemPermissions
                                     ▲
                                     │
                                  MenuItems  >── MenuModules (self-ref)
```

## Backend (SIS.Infrastructure + SIS.API)

### Servis Katmanı ([SIS.Infrastructure/Services/](../../SIS.Infrastructure/Services/))

| Servis | Sorumluluk | Cache |
|--------|-----------|-------|
| [PermissionService.cs](../../SIS.Infrastructure/Services/PermissionService.cs) | `GetUserPermissionsAsync`, `HasPermissionAsync`, `SetRolePermissionsAsync`, `InvalidateUserCache`, `InvalidateRoleCacheAsync` | IMemoryCache 5dk TTL, key=`permissions:user:{userId}` |
| [MenuService.cs](../../SIS.Infrastructure/Services/MenuService.cs) | `GetUserMenuAsync` (recursive in-memory tree, OR-permission filter, empty module elimination) | DbContext direkt; AsSplitQuery + AsNoTracking |

### Authorization Attribute ([SIS.API/Authorization/](../../SIS.API/Authorization/))

[`RequirePermissionAttribute.cs`](../../SIS.API/Authorization/RequirePermissionAttribute.cs) — 3 katmanlı check:
1. **Unauthenticated** → 401
2. **SystemAdministrator rolü** → izin (DB lookup yapılmaz, en hızlı yol)
3. **`lookup.*` key** → izin (hardcoded shortcut, her authenticated user)
4. **`PermissionService.HasPermissionAsync`** → DB/cache lookup

### Controller'lar ([SIS.API/Controllers/](../../SIS.API/Controllers/))

51 controller × ~310 endpoint hepsi `[RequirePermission("...")]` ile korunuyor (Sprint 5'te migrate). 3 controller hariç tutuldu:
- `AuthController/login` — `[AllowAnonymous]`
- `AnnouncementController/scrape` — `[AllowAnonymous]` (public scrape)
- `UserPreferencesController` — `[Authorize]` (sadece authenticated, permission yok)

### API Endpoint'leri (RBAC için)

| Endpoint | Açıklama | Permission |
|----------|----------|-----------|
| `GET /api/me/permissions` | Kullanıcının tüm permission key'leri | `[Authorize]` (her authenticated) |
| `GET /api/me/menu` | Kullanıcının dinamik menüsü (permission-filtered) | `[Authorize]` |
| `GET /api/admin/permissions` | Tüm DB-seed permission'lar (admin matrisi) | `system.permission.view` |
| `GET /api/admin/permissions/roles` | Atanabilir roller (SysAdmin hariç) | `system.permission.view` |
| `GET /api/admin/permissions/roles/{id}` | Rol yetki durumu (`{ manualPermissionIds, bundlePermissionIds }`) | `system.permission.view` |
| `PUT /api/admin/permissions/roles/{id}` | Rolün **manuel** permission'larını TOPLU set (paket satırları korunur — `SourceBundleId IS NULL` only) | `system.permission.assign` |
| `GET /api/admin/roles/{id}/applied-bundles` | Role uygulanmış paketler + `isStale` | `system.permission-bundle.view` |
| `POST /api/admin/permission-bundles/{id}/apply/{roleId}` | Paketi role uygula / yeniden uygula | `system.permission-bundle.apply` |
| `DELETE /api/admin/permission-bundles/{id}/apply/{roleId}` | Paketi rolden kaldır | `system.permission-bundle.apply` |

### JWT (v2 Karar 6)

JWT'ye permission claim **EKLENMEZ**. Permission listesi `/api/me/permissions` endpoint'inden cache'li çekilir. JWT boyutu sabit kalır (~640 bytes), permission değişikliği max 5 dk içinde yansır.

## Frontend (SIS.Web)

### Context ([SIS.Web/src/context/](../../SIS.Web/src/context/))

[`PermissionContext.tsx`](../../SIS.Web/src/context/PermissionContext.tsx) — 5 dk staleTime ile `/api/me/permissions` cache'ler. SystemAdmin shortcut.

### Hook'lar ([SIS.Web/src/hooks/](../../SIS.Web/src/hooks/))

| Hook | Kaynak | Açıklama |
|------|--------|----------|
| `usePermissions()` | PermissionContext | `hasPermission(key)`, `hasAnyPermission(keys[])`, `isSystemAdmin` |
| `useMenu()` | [useMenu.ts](../../SIS.Web/src/hooks/useMenu.ts) | Backend menü → frontend `Module[]`; ICON_MAP fallback HelpCircle; `slugify()` Türkçe karakter normalize |
| `useAllPermissions()`, `useAssignableRoles()`, `useRolePermissions()`, `useUpdateRolePermissions()` | [useRolePermissions.ts](../../SIS.Web/src/hooks/useRolePermissions.ts) | Admin matrix için |

### Bileşenler

| Component | Dosya | Açıklama |
|-----------|-------|----------|
| `RoleProtectedRoute` | [RoleProtectedRoute.tsx](../../SIS.Web/src/components/RoleProtectedRoute.tsx) | Permission-based: `permission` / `anyPermission` prop'ları (eski `allowedRoles` 2026-04-26'da silindi). Detay aşağıda "Frontend Route Guards" bölümünde. |
| `Sidebar` | [Sidebar.tsx](../../SIS.Web/src/components/layout/Sidebar.tsx) | `useMenu()` data layer, render kısmı (favorites injection, accordion, flyout) korundu |
| `RolePermissionMatrix` | [admin/RolePermissionMatrix.tsx](../../SIS.Web/src/components/admin/RolePermissionMatrix.tsx) | "Kullanıcı Yetki Grupları" sayfasının **"Rollere Yetki Ata"** sekmesinde render edilir (Phase 2'de prod'a alındı). Bundle yetkileri kilitli (salt-okunur), manuel yetkiler düzenlenebilir; üstte uygulanmış paket paneli (kaldır / yeniden uygula / stale rozeti). |
| `ForbiddenPage` | [ForbiddenPage.tsx](../../SIS.Web/src/pages/ForbiddenPage.tsx) | 403 sayfası |

### Kullanıcı Yetki Grupları sayfası (Phase 2)

[`UserPermissionGroupsPage.tsx`](../../SIS.Web/src/pages/UserPermissionGroupsPage.tsx) iki sekme: **Yetki Paketleri** (bundle CRUD) + **Rollere Yetki Ata** (`RolePermissionMatrix`). İkinci sekme `system.permission.view` yetkisi varsa görünür. Route + menü `system.permission-bundle.view` ile korunur (eski `menu.kullanici-yetki-gruplari.*` anahtarları kaldırıldı — bkz. git geçmişindeki `scripts/2026-05-26-permission-bundle-perm-standardize.sql`; tek seferlik betikler Adım 3.9da kaldırıldı). Eski `/__dev/permission-matrix` dev-only route'u silindi.

### SidebarModules.ts (slim)

Sprint 3'te 7 hardcoded role array silindi (-449 satır). Sadece type'lar + helper'lar + `ICON_MAP` + `resolveIcon` kalır. Backend menü Tek Kaynak.

## Frontend Route Guards (Updated 2026-04-26)

**Tüm `App.tsx` route'ları permission-based çalışır:**

```tsx
<Route element={<RoleProtectedRoute permission="menu.fakulte-tanimlari.view" />}>
  <Route path="fakulte-tanimlari" element={<FacultyDefinitionsPage />} />
</Route>
```

**Eski hardcoded role check kaldırıldı:**
- `RoleProtectedRoute` komponentinde sadece `permission` ve `anyPermission` prop'ları var
- `allowedRoles` prop'u Sprint 5 geçiş döneminin kalıntısıydı, 2026-04-26'da silindi
- Hiçbir route artık `STUDENT_ROLES`, `MANAGEMENT_ROLES`, `DEAN_ROLES` gibi hardcoded liste kullanmıyor
- `lib/roles.ts` constants hâlâ duruyor (sidebar `getPrimaryRole`, dashboard `isManagementRole` için) ama route koruması için kullanılmıyor

**Davranış:**
1. `permission` prop'taki key kullanıcıda varsa → `<Outlet />` (sayfa render)
2. `anyPermission` set'inden birinde varsa → `<Outlet />`
3. Kullanıcı yetkisizse → `/forbidden` redirect
4. Permission yüklenirken → skeleton loader (flash önlemi)

**SystemAdmin shortcut:** `PermissionContext.hasPermission` her zaman true döner — DB lookup yapılmaz. Bu sayede admin UI'dan eklenen yeni bir sayfa, kod değişmeden SystemAdmin'e açılır.

**Yeni route eklerken:**
1. Permission key seç (genelde `menu.<slug>.view`)
2. Anahtar `PermissionSeed.cs`'in `All` listesinde olmalı (yoksa ekle, restart sonrası DB'ye merge olur)
3. Route'u sar:
   ```tsx
   <Route element={<RoleProtectedRoute permission="menu.<slug>.view" />}>
     <Route path="..." element={<...Page />} />
   </Route>
   ```
4. İlgili rollere yetki ata: admin UI'dan veya `RolePermissionSeed.cs`'te seed default
5. (Eğer menu item ise) `Persistence/Katalog/MenuKatalogu.cs` listesine ekle, item'ın permission anahtarını yaz

**Çoklu permission gerekliyse** (nadir): `anyPermission={[...]}` kullan, OR mantığı.

## Seed (Sprint 1.4)

[`SIS.Infrastructure/Persistence/Seeds/`](../../SIS.Infrastructure/Persistence/Seeds/):

| Seed | Dosya | Sayı | Notlar |
|------|-------|------|--------|
| `PermissionSeed` | [PermissionSeed.cs](../../SIS.Infrastructure/Persistence/Seeds/PermissionSeed.cs) | 255 key | Liste; yazma işi sistem kataloğunda (`Persistence/Katalog/SistemKatalogu.cs`): eksik key'i ekler, kategori ve açıklamayı katalogdaki değere çeker, silinmiş key'i geri getirmez |
| `MenuKatalogu` | [MenuKatalogu.cs](../../SIS.Infrastructure/Persistence/Katalog/MenuKatalogu.cs) | 18 modül + 74 öğe | Eksik olanı ekler, yöneticinin sildiğini geri getirmez (eski `MenuStructureSeed` yalnız eşdeğerlik testi için duruyor) |
| `RolePermissionSeed` | [RolePermissionSeed.cs](../../SIS.Infrastructure/Persistence/Seeds/RolePermissionSeed.cs) | 6 rol, 432 atama | Liste; yazma işi rol-izin kataloğunda (`Persistence/Katalog/RolIzinKatalogu.cs`). SystemAdmin **YOK** (locked) |

`SeedData.SeedAsync` orchestrator [SeedData.cs:23-26](../../SIS.Infrastructure/Persistence/SeedData.cs) içinde sırayla çağırır.

## Kararlar (Locked)

| # | Karar | Kısa Açıklama |
|---|-------|---------------|
| 1 | Permission naming | Menu-merkezli (`menu.<slug>.<action>`) |
| 2 | Menu source | Pure DB (SidebarModules.ts hardcoded array'leri silindi) |
| 3 | Granularity | Hem menü hem endpoint |
| 4 | SystemAdmin | Locked, kod-shortcut, admin UI'da hiç gözükmez |
| 5 | Migration | Transition mode (Sprint 1-3 yan yana, Sprint 5 cleanup) |
| 6 | JWT permission claim | YOK — frontend `/api/me/permissions` cache eder (5dk) |
| 7 | `lookup.*` | DB seed YOK, hardcoded shortcut (her authenticated user) |
| 8 | `system.menu.*` | Phase 1'de yok (Phase 4'e ertelendi) |
| 9 | Admin UI lokasyonu | Phase 1'de sidebar'a EKLENMEZ, Phase 2'de import edilir |
| 10 | Audit logging | Phase 1'de YOK |

## Ölçümler (Sprint Final)

| Metrik | Değer | Hedef |
|--------|-------|-------|
| JWT boyutu (avg) | ~640 bytes | < 4KB |
| JWT boyutu (max) | 667 bytes | < 8KB |
| `/api/me/permissions` cache miss | ~58ms | — |
| `/api/me/permissions` cache hit | ~3ms | — |
| `/api/me/menu` cache miss | ~120ms | < 500ms |
| `/api/me/menu` cache hit | ~9ms | < 200ms |

## Test Coverage

| Layer | Dosya | Test sayısı |
|-------|-------|-------------|
| `PermissionService` | [PermissionServiceTests.cs](../../SIS.Tests/Application/PermissionServiceTests.cs) | 6 |
| `MenuService` | [MenuServiceTests.cs](../../SIS.Tests/Application/MenuServiceTests.cs) | 5 |
| `RequirePermissionAttribute` | [RequirePermissionAttributeTests.cs](../../SIS.Tests/Authorization/RequirePermissionAttributeTests.cs) | 6 |
| **Toplam** | | **17** (hepsi yeşil) |

## Operasyonel

### Orphan Detection Script

[`scripts/check-permission-orphans.sh`](../../scripts/check-permission-orphans.sh) — codebase'de kullanılan permission key'leri ile DB'deki Permissions tablosunu karşılaştırır:
- Eksik DB key (kritik) → PermissionSeed.All'a ekle, API restart
- Orphan DB key (risksiz) → cleanup adayı

### İzin kataloğu eşitlemesi

Yeni permission key eklenince (controller'da `[RequirePermission("yeni.key")]`):
1. PermissionSeed.cs'in `All` listesine ekle
2. `setup` (ya da Development açılışı) → sistem kataloğu eksik key'i INSERT eder; var olanın kategori ve açıklamasını günceller; bir kez yazılmış ama silinmiş key'i geri getirmez
3. Mevcut RolePermission ilişkileri korunur

### Cache Invalidation

Admin UI'dan permission değiştirince:
1. `PUT /api/admin/permissions/roles/{id}` çağrısı
2. Backend: `PermissionService.SetRolePermissionsAsync` → ilgili rolün TÜM kullanıcılarının cache'i drop edilir (`AspNetUserRoles`'dan UserId çekilir)
3. Frontend: `useUpdateRolePermissions` mutation onSuccess → `permissions` ve `menu` query cache'leri invalid
4. Etkilenen kullanıcılar sayfayı yenilediğinde max **anlık** olarak güncel veriyi görür

## Permission Scope (Phase 2)

Phase 2 ile RBAC sistemi yalnızca "permission var/yok" değil, "hangi veri üzerinde" boyutunu da taşır.
Şu an sadece `Dean` scope-bound roldür ve kullanıcıya `UserScopes` tablosu üzerinden tek fakülte atanır.

- Read tarafı: `AppDbContext` query filter'ları `Faculty`, `Department`, `AcademicProgram`, `Curriculum`,
  `Course`, `CourseSection`, `Student`, `Instructor`, `ElectiveGroup`,
  `CurriculumElectivePool` için faculty kısıtı uygular.
- Write tarafı: `IScopeGuard` create/cross-faculty update akışlarında 403 üretir.
- Bypass: `SystemAdministrator` için scope pasiftir; admin operasyonlarında gerektiğinde `WithoutScope()`
  helper'ı kullanılabilir.
- Admin UI: `GET/PUT/DELETE /api/admin/users/{id}/scope` ve kullanıcı yönetimi modalındaki
  "Kapsam (Fakülte)" alanı ile scope atanır.

## Sonraki Phase'ler (Plan'da, bu codemap kapsamında değil)

- **Phase 2 (tamamlandı):** Permission scope / faculty-level data filtering
- **Phase 3:** Vekalet (delegation)
- **Phase 4:** Modül editörü (admin yeni modül oluşturabilir, `system.menu.*` permission'ları)
- **Phase 5:** Rapor grupları
