> Taşındığı tarih: 2026-11-15
> Kaynak: CLAUDE.md @ e7da529f (satır 24–245, değiştirilmeden)
## Key Business Rules (Course Registration)
All thresholds below are configurable via SystemConfig (admin panel), values shown are defaults:
- ECTS caps: CGPA-based limits (defaults: <2.00 → 30, 2.00-2.49 → 42, ≥2.50 → 48)
- Retention threshold (default: 1.70) → student retained after 1st semester, cannot take upper-level courses
- FF/NA/U courses must be re-selected before new courses
- Full overlap for first-time courses → categorically blocked, no override
- Full/Partial overlap for repeated (FF) courses → allowed with advisor + dept manager approval
- Instructor cannot be assigned to overlapping sections
- Registration plan: soft hold on capacity, cleared at midnight if unconfirmed

## Term Lifecycle (Faz 5 — flag-bazlı tek-aktif model, Proliz/Banner)
Inactive → Active → Cancelled

- **Aktiflik manuel bayrağa bağlıdır, tarihe DEĞİL** (Proliz/Banner "güncel dönem" modeli). 2026-05-23'te tarih-bazlı hibrit model kaldırıldı.
- `TermStatus` enum **gösterim** için (stored değil). Durum `term.GetStatus(now)` ile hesaplanır.
- Stored manuel alanlar: `IsActivated: bool` (**default false** — yeni dönem kapalı başlar), `IsCancelled: bool`, `OverrideStart?`, `OverrideEnd?` (override'lar artık aktifliği etkilemez; sadece EffectiveStart/End computed alanlarını besler, dormant).
- `GetStatus(now)`: `IsCancelled` → Cancelled; `IsActivated` → Active; aksi halde Inactive. **TermStart/TermEnd aktifliği belirlemez.**
- `IsActive(now) = !IsCancelled && IsActivated`.
- **Tek-aktif değişmezi**: `ActivateTermAsync(id)` bu dönemi açar **+ diğer tüm açık dönemleri kapatır** → her an en çok bir dönem açık. CreateTermAsync/generator dönemi kapalı oluşturur.
- **"Aktif dönem" sorgusu TEK kaynak**: `AcademicTerms.ActiveTerm()` extension (`SIS.Domain/Entities/AcademicTermQueryExtensions.cs`) = `Where(!IsCancelled && IsActivated).OrderByDescending(TermStart)`. Önceki ~33 inline kopya (`...&& TermStart<=now && TermEnd>=now`) bununla değiştirildi.
- IsActivated: admin "Kullanıma Aç/Kapat" ile değiştirir (activate/deactivate endpoint'leri).
- Cancelled: admin iptal etmiş (manuel). Cascade: tüm section'lar Cancelled olur.
- Unique constraint: `(Year, TermType)` — aynı yıl/tip kombinasyonu tek kayıt.
- Migration: `SingleActiveTermFlagModel` (kolon default false + tek seferlik tek-aktif veri normalizasyonu).

## TermType (9 değer — Ankara Bilim pattern)
Fall, Spring, Summer, FallResit, SpringResit, SummerResit, SpringOther, SummerOther, Exemption

- Eski `SemesterType` enum silindi; semester bilgisi TermType'a birleşti.
- TermType default: Fall.

## Section Lifecycle (Faz 1 — otomatik model)
Draft ↔ Active ↔ Cancelled

- 3 durumlu enum `CourseSectionStatus`, **stored** (index ile sorgulanır).
- Draft ↔ Active otomatik: hoca + derslik + slot dolunca Active, biri eksilince Draft.
- Cancelled: admin'in manuel aksiyonu (CloseSection / ReactivateSection endpoint'leri).
- Geriye uyumluluk: `IsActive` computed property `=> Status == Active`. LINQ sorgularında `cs.Status == CourseSectionStatus.Active` kullanılır (EF translate için).

## Akademik Takvim (Faz 2 — entity split)
7 tarih + 4 override alanı `AcademicTerm`'den ayrılıp yeni `TermAcademicCalendar` entity'sine taşındı (1-1 ilişki, cascade delete, unique FK).

- **Stored `AcademicTerm` alanları**: kimlik + `TermStart`/`TermEnd` + `IsCancelled` + `YoksisStatus`
- **Stored `TermAcademicCalendar` alanları**: 7 tarih (RegistrationStart/End, AddDropStart/End, WithdrawalDeadline, GradeEntryStart/End) + 4 override (RegistrationOverride, AddDropOverride, WithdrawalOverride, GradeEntryOverride) + CreatedAt/UpdatedAt
- **Navigation**: `AcademicTerm.AcademicCalendar?` (nullable — takvim opsiyonel; term oluşunca yok, admin sonradan ekler)
- **Include zorunlu**: `term.IsRegistrationOpen(now)` vb. çağıran her query `.Include(t => t.AcademicCalendar)` içermeli. AcademicCalendar null ise phase method'ları `false` döner (silent close — veri tutarsızlığı kullanıcıya "faz kapalı" olarak yansır).

## Term Phases (computed, state değil)
- IsRegistrationOpen: IsActive(now) + AcademicCalendar != null + (override ?? now ∈ [RegStart, RegEnd])
- IsAddDropOpen: IsActive(now) + AcademicCalendar != null + (override ?? now ∈ [AddDropStart, AddDropEnd])
- IsWithdrawalOpen: IsActive(now) + AcademicCalendar != null + (override ?? TermStart ≤ now ≤ WithdrawalDeadline)
- IsGradeEntryOpen: IsActive(now) + AcademicCalendar != null + (override ?? now ∈ [GradeEntryStart, GradeEntryEnd])
- Override null → takvim konuşur; true/false → admin override
- Cancelled dönemde override değiştirilemez.

## Admin Term API (Faz 1 revize + Faz 2)
- `GET /api/admin/terms` — tüm dönemler
- `GET /api/admin/terms/{id}` — tek dönem
- `POST /api/admin/terms` — yeni dönem (TimeSlot grid'i otomatik oluşturulur, AcademicCalendar null kalır)
- `PUT /api/admin/terms/{id}` — düzenleme (Cancelled dönem düzenlenemez)
- `POST /api/admin/terms/{id}/activate` — kullanıma aç (IsActivated = true)
- `POST /api/admin/terms/{id}/deactivate` — kullanıma kapat (IsActivated = false)
- `POST /api/admin/terms/{id}/cancel` — iptal + section cascade
- `POST /api/admin/terms/{id}/uncancel` — iptali geri al
- Silinmiş endpoint'ler: `/transition`, `/override` (faz override Akademik Takvim API'ye taşındı)
- Not: OverrideStart/OverrideEnd (erken aktivasyon/uzatma) backend Create/Update DTO'larında var ama TermManagementPage formu bu alanları yönetmez (UI'dan girilmez).

## Academic Calendar API (Faz 2 — yeni)
Admin endpoints:
- `GET /api/admin/academic-calendar` — tüm dönemlerin takvim özet listesi (Auth: SystemAdministrator, AdministrativeStaff)
- `GET /api/admin/academic-calendar/{termId}` — belirli dönemin takvimi (404 if no calendar)
- `PUT /api/admin/academic-calendar/{termId}` — upsert (Auth: SystemAdministrator)
- `POST /api/admin/academic-calendar/{termId}/override` — faz override (Auth: SystemAdministrator)

Public endpoints (any authenticated):
- `GET /api/academic-calendar/active` — aktif dönemin öğrenci görünümü
- `GET /api/academic-calendar/{termId}` — belirli dönem öğrenci görünümü
- `GET /api/academic-calendar/terms` — dönem listesi (hasCalendar flag'iyle)

Cancelled dönem: upsert ve override `InvalidOperationException` fırlatır (400).

## ÖSYM + Kontenjan placeholder sayfaları (SİLİNDİ — 2026-05-23)

"ÖSYM Kayıt Takvimi" ve "Program Kontenjan Bilgileri" stub sayfaları backend + frontend
tamamen kaldırıldı (entity, DTO, service, controller, page, hook, route, menu, permission).
Tablolar `OsymRegistrationCalendars` + `ProgramQuotas` migration ile düşürüldü
(`DropOsymRegistrationAndProgramQuota`); mevcut DB satırları
`scripts/2026-05-23-remove-osym-program-kontenjan.sql` ile temizlendi.

## AcademicTerm Yeni Alanlar (Faz 1)
- `ShortEnglishName: string` — kısa İngilizce ad (zorunlu, default empty)
- `OtherName: string?` — diğer adı (opsiyonel)
- `YoksisStatus: YoksisStatus` — YÖKSİS raporlama durumu (Inactive/Active)

## Sidebar "Akademik İşlemler" grubu (Faz 4 güncel)
- Akademik Dönem Tanımları (`/donem-yonetimi` — TermManagementPage, form 11 alan: kimlik + TermStart/TermEnd)
- Fakülte Tanımları (`/fakulte-tanimlari` — FacultyDefinitionsPage) **Faz 3**
- Bölüm Tanımları (`/bolum-tanimlari` — DepartmentDefinitionsPage) **Faz 3**
- Program Tanımları (`/program-tanimlari` — ProgramDefinitionsPage) **Faz 3**
- Akademik Takvim Yönetimi (`/akademik-takvim-yonetimi` — AcademicCalendarAdminPage) **Faz 2**
- Akademik Takvim (Görünüm) (`/akademik-takvim` — AcademicCalendarPage, tabs: Liste + Takvim) **Faz 2**
- Takvim Oluşturucu (`/akademik-takvim-olusturma` — AcademicCalendarGeneratorPage)
- Sınıf Atlatma Kuralları (`/sinif-atlatma-kurallari` — PromotionRulesPage)

Eski gruplar ("Dönem Yönetimi", "Akademik Yapı", "Fakülte / Bölüm / Program" tek sayfa) silindi.

## Elective Pool Refactor (Faz 6 — TAMAMLANDI 2026-04-27/28)

Seçmeli ders sistemi flat `GroupCode` etiketinden gerçek pool/slot model'ine geçti.
Branch `fix/curriculum-management-page` üzerinde 29+ ardışık commit, push'suz.
Tam commit haritası + 14 davranış kararı için: kullanıcı memory dosyası
`project_elective_pool_refactor.md`.

### Mimari (TL;DR)
- **`ElectiveGroup`** (havuz tanımı, ders listesi) — UI: "Seçmeli Ders Grubu". DepartmentId scope ladder (okul/fakülte/bölüm). FK `ElectiveSubtypeId` (NOT NULL, DB-driven tip; deprecated enum kolon Faz E'de söküldü).
- **`CurriculumElectivePool`** (müfredata bağlanma slot kaydı) — UI: "Grup". `SlotName` (TE-1, TE-2…), `EctsPerCourse` override. Unique `(CurriculumId, ElectiveGroupId, SlotName)`.
- **`ElectiveSubtypeEntity`** (TE/NDE/OC + custom tipler) — tek kaynak. DB-driven CRUD admin sayfası `/secmeli-yapilandirma/tipler`. Code immutable, Name + 4 scope flag editable. Frontend display `lib/electiveSubtypeUtils` (Code-based renk + name lookup).
- **`ApplicablePoolSlotDto`** — öğrencinin `student-catalog` cevabında her ders için aday olduğu slot listesi.

### Ana sayfalar
- Admin müfredat detayı (`/mufredat-islemleri` → ders listesi): ders satırları + grup slot satırları aynı tabloda karışık (Proliz tarzı). "+ Grup Ekle" butonu AddPoolDialog açar; pool satırına tıklama PoolDetailDialog (inline edit + aday liste + sil onayı).
- Admin seçmeli tipleri (`/secmeli-yapilandirma/tipler`): SysAdmin yeni tip ekler/düzenler. List endpoint multi-role (Patch 2A — read-only lookup).
- Öğrenci ders kayıt (`/ders-kayit`): üstte "Müfredat Slotlarım" kartları (CourseCatalogPage), slot tıklayınca aday dersler dialog'u. "Üstten yarıyıl havuzları" toggle (default kapalı).
- Öğrenci kayıt planı (`/ders-kayit-plani`): submit'te eksik slot warning AlertDialog.
- Öğrenci müfredat durumu (`/mufredat-durum`): pool fulfillment kartları SlotName ile.

### Kritik kararlar (memory'de tam liste)
- **#1** Submit gate yumuşak (warning, danışman süzer)
- **#5** Bir ders birden fazla pool'a aday olabilir (`applicablePoolSlots` çoğul)
- **#13** Grup oluşturma yetkisi: SysAdmin + Dean + Bölüm Başkanı
- **#14** ElectiveSubtype Code immutable, sadece Name editable

### Faz E (TAMAMLANDI 2026-04-28)
Deprecated `ElectiveGroup.ElectiveSubtype` enum kolonu DROP migration uygulandı. `SIS.Domain/Enums/ElectiveSubtype.cs` dosyası silindi. Frontend `ELECTIVE_SUBTYPE_LABELS/SHORT/COLORS` helper'ları kaldırıldı, display tamamen `lib/electiveSubtypeUtils` üzerinden DB-driven (custom subtype'lar için doğru name + Code-based renk fallback). Refactor closed.

### Faz 6 Post — UI Birleştirme (TAMAMLANDI 2026-04-29)
İki ayrı sidebar item ("Seçmeli Ders Grupları" + "Seçmeli Tipleri") tek sayfada tab'lara birleştirildi: `/secmeli-yapilandirma/{gruplar|tipler}`. Mevcut sayfalar (`ElectiveGroupManagementPage`, `ElectiveSubtypeManagementPage`) içerik olarak korunuyor; wrapper `ElectiveConfigurationPage` permission-gated tab visibility ve tek-tab edge-case'ini handle ediyor. Yeni meta-permission `menu.secmeli-yapilandirma.view` 3 role'e seed edildi (SysAdmin/Dean/DepartmentManager). Eski permission key'leri (`menu.secmeli-ders-gruplari.*`, `menu.secmeli-tipleri.*`) korundu — API guard'ları ve tab visibility için hâlâ kullanılıyor. Eski route'lar `<Navigate>` ile redirect oluyor. SQL migration: `scripts/2026-04-29-secmeli-yapilandirma-merge.sql`.

## RBAC (Phase 1 — DB-driven Permissions, tag: `rbac-phase1-mvp`)

Yetki sistemi DB-driven, admin UI'dan deployment'sız değişebilir. Tüm controller'lar `[RequirePermission("...")]` ile korunur (eski `[Authorize(Roles=...)]` kalkmıştır).

### Permission key formatı
```
<category>.<resource>.<action>
```
- `menu.*` — sidebar item'lara bağlı (DB seed, ~237 key)
- `lookup.*` — dropdown lookup (DB seed YOK, hardcoded shortcut: her authenticated)
- `system.*` — admin UI (DB seed, ~2 key, sadece SysAdmin)

### Authorization akışı
1. Backend: `[RequirePermission("menu.akademik-donem.view")]` → 3 katmanlı check (SysAdmin shortcut → lookup.* shortcut → DB cache lookup)
2. Frontend: `usePermissions().hasPermission("menu.akademik-donem.view")` → SysAdmin shortcut + 5dk cache'li `/api/me/permissions`
3. Sidebar: `useMenu()` → DB-driven dinamik menü (permission-filtered)

### SystemAdmin (Karar 4 — locked)
- DB'de RolePermission satırı YOK
- `RequirePermissionAttribute` ve `PermissionService.GetUserPermissionsAsync` kod-shortcut: `IsInRole("SystemAdministrator")` ise tüm permission'lara erişim
- Admin UI rol listesinde HİÇ GÖZÜKMEZ

### JWT (Karar 6)
JWT'ye permission claim **eklenmez** (~640 bytes sabit). Frontend `/api/me/permissions` endpoint'inden 5dk cache ile çeker. Permission değişikliği max 5dk sonra refresh'te yansır.

### Yeni endpoint eklerken
1. Controller method'a `[RequirePermission("menu.<slug>.<action>")]` ekle
2. Permission key DB'de yoksa: `PermissionSeed.cs`'in `All` listesine ekle (merge semantiği — API restart sonrası otomatik DB'ye eklenir)
3. Default seed'e dahil etmek istiyorsan: `RolePermissionSeed.cs`'te ilgili rol(ler)e ekle (yeni rol-permission satırları için DB temizleme + re-seed gerekir, ya da manuel SQL INSERT)
4. Orphan check: `bash scripts/check-permission-orphans.sh`

### Frontend Route Protection (Updated 2026-04-26)

`App.tsx`'teki tüm route'lar `<RoleProtectedRoute permission="menu.xxx.view">` kullanır — DB-driven, hardcoded role listesi yok. SystemAdmin shortcut `PermissionContext` üzerinden otomatik. Sprint 5'in kalan frontend kısmı 2026-04-26'da kapatıldı (bkz. `docs/superpowers/plans/2026-04-26-rbac-frontend-permission-routes.md`).

Eski `<RoleProtectedRoute allowedRoles={...}>` deseni artık desteklenmiyor — yeni sayfa eklerken sadece `permission` veya `anyPermission` prop'u kullanın.

### Codemap
Tam detay: [docs/CODEMAPS/permission-system.md](docs/CODEMAPS/permission-system.md)

### Ana dosyalar
- Backend: `SIS.API/Authorization/RequirePermissionAttribute.cs`, `SIS.Infrastructure/Services/PermissionService.cs`, `MenuService.cs`
- Frontend: `SIS.Web/src/context/PermissionContext.tsx`, `hooks/useMenu.ts`, `components/admin/RolePermissionMatrix.tsx`
- Seed: `SIS.Infrastructure/Persistence/Seeds/{Permission,MenuStructure,RolePermission}Seed.cs`

### Permission Scope (Phase 2)

Yetki sistemi artık ortogonal bir "scope" boyutuna da sahip. Scope-bound rol üyesi kullanıcılar
şu an sadece `Dean` yetkili oldukları işlemleri yalnızca atanmış fakülte verisi üzerinde yapabilir.

- Stored: `UserScopes` tablosu (`UserId`, `Kind`, `FacultyId`)
- Read: `AppDbContext` içindeki EF query filter'ları `_scope.IsActive` aktifken otomatik faculty filtresi uygular
- Write: `IScopeGuard.EnsureCanWrite(ScopeKind.Faculty, targetFacultyId)` create ve cross-faculty update akışlarında explicit çağrılır
- Resolve helper: `IScopeResolver` department/program/course gibi FK zincirlerinden faculty çözer
- SystemAdmin her zaman bypass eder (`ICurrentUserScope.IsActive == false`)
- Bilinçli bypass: `query.WithoutScope()` sadece admin/cross-faculty operasyonlarında kullanılmalı

Yeni bir entity faculty'e doğrudan veya dolaylı bağlıysa, scope gerekip gerekmediği kontrol edilip
gerekliyse `OnModelCreating` query filter zincirine eklenmeli.

## Rapor Motoru (Faz 1 — handler registry + tek çatı)

Raporlar tek ortak motorda çalışır; ikibaşlılık (her rapora ayrı sayfa/export) yok.

- **Yeni rapor = tek `IReportHandler` sınıfı** (`SIS.Application/Reports/IReportHandler.cs`). `Code` (ReportDefinition.Code ile birebir) + `GetParameterSchema()` + `RunAsync(request, ctx, ct)` → `ReportResult` (kolon+satır). DI'da otomatik taranır (Program.cs handler scan).
- **Çalıştırma:** `IReportExecutionService` (`ReportExecutionService`) → kod→handler, `CanAccessReportAsync` yetki, parametre, scope (`ICurrentUserScope` → `ReportExecutionContext.ScopeFacultyIds`), sayfalama.
- **Export:** tek `IReportExportService` (`ReportExportService`) → ClosedXML (.xlsx) / QuestPDF (.pdf).
- **Endpoint'ler:** `GET /api/me/reports/{code}/schema`, `POST .../{code}/run`, `POST .../{code}/export?format=xlsx|pdf` (`ReportRunController`). Rapor listesi: `GET /api/me/reports` (ReportAuthorizationController).
- **Frontend:** tek çatı — `MyReportsPage` katalog → `ReportRunPanel` (Sheet) → `ReportFilterForm` (şemadan otomatik) → `ReportResultTable` (+ Excel/PDF). `reportRun.api.ts` + `useReportRun.ts`.
- **Seed:** handler'ı olan raporlar `ReportSeed` ile idempotent eklenir (kategori `genel-raporlar`). İlk rapor: `ogrenci-listesi` (`StudentListReportHandler`).
- **Yetkilendirme (Faz 2):** birleşik `ReportAccessGrant` — *(ScopeKind: Category|Report) × (SubjectKind: Role|User|Group) × (Effect: Allow|Deny)*. Tek `IReportAccessResolver` (öncelik: rapor-Deny > rapor-Allow > kategori-Deny > kategori-Allow > yok); `CanAccessReportAsync` + `ListAccessibleReportsAsync` buna delege. `ReportUserGroup`(+Member) = adlandırılmış kullanıcı kümeleri. Eski `ReportCategoryRolePermission` + `ReportPermissionOverride` migration (`AddReportAccessGrantUnifiedModel`) ile grant'lara taşınıp düşürüldü. Admin uçları: `/api/admin/report-grants`, `/api/admin/report-user-groups`. SystemAdmin bypass. Tasarım: `docs/superpowers/specs/2026-05-26-rapor-sistemi-obs-overhaul-design.md`.

## Code Conventions
- Domain entities: plain C# classes, no EF Core imports
- ApplicationUser (IdentityUser) lives in SIS.Infrastructure/Persistence/
- Schedule data stored in SectionSchedule table
- Composite PKs (CoursePrerequisite, CourseEquivalency, CurriculumCourse) configured in OnModelCreating
- SystemConfig table for configurable business rules (no hardcoded values)
- ISystemConfigProvider with 5-minute memory cache for config reads

## Database
- PostgreSQL, Host: localhost, Port: 5432, Database: StudentInfoSystem
- appsettings.json: placeholder değerler (CHANGE_ME) — GitHub'a gider
- appsettings.Development.json: gerçek şifreler — .gitignore'da, GitHub'a gitmez
