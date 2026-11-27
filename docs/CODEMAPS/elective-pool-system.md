# Elective Pool System (Faz 6) — Codemap

<!-- Generated: 2026-04-28 | Faz 6 refactor: 29+ commits (A-E hepsi) | Status: TAMAMLANDI -->

> **Faz 6 Elective Pool Refactor** — Seçmeli ders sistemi flat `GroupCode` etiketinden gerçek pool/slot model'ine geçti. Faz E'de deprecated enum kolonu/dosyası tamamen söküldü.
> Branch: `fix/curriculum-management-page` (push'suz, 29+ ardışık commit).
> Memory: kullanıcı dotfile `project_elective_pool_refactor.md` (commit haritası + 14 davranış kararı).

## Hızlı Tanıtım

| Soru | Cevap |
|------|-------|
| Müfredata grup nasıl bağlanır? | `CurriculumElectivePool` slot kaydı (`SlotName: TE-1`, vs.). Aynı grup farklı slot adlarıyla N kez bağlanabilir. |
| Seçmeli tip (TE/NDE/OC) nereden? | `ElectiveSubtypes` DB tablosu — admin yeni tip ekleyebilir ("Sosyal Seçmeli"). Kod sabit, Türkçe ad + scope flag'leri editable. |
| Bir ders birden fazla pool'a aday olabilir mi? | Evet (Karar #5). `ApplicablePoolSlotDto` çoğul liste döner. |
| Pool fulfillment nasıl hesaplanır? | `CourseRegistration.AppliedElectivePoolId` snapshot ile pool'a sayılır; `EctsSnapshot` veya ders kataloğu AKTS toplanır. |
| Grup oluşturma yetkisi? | SysAdmin + Dean + Bölüm Başkanı (Karar #13). Tip yönetimi sadece SysAdmin (Karar #1). |

## Veri Modeli (4 entity)

```
┌─────────────────────┐         ┌──────────────────────────┐
│ ElectiveSubtypeEntity│        │ ElectiveGroup             │
│  Id, Code, Name     │◄────────┤  Id, Code, Name           │
│  RequiresFaculty    │ FK      │  ElectiveSubtypeId (NOT N)│
│  ForbidsFaculty     │         │  FacultyId? + DepartmentId│ ← scope ladder
│  RequiresDept       │         │  IsActive                 │
│  ForbidsDept        │         │                           │
└─────────────────────┘         └──────┬───────────────────┘
                                       │
                  ┌────────────────────┼────────────────────┐
                  │                    │                    │
                  ▼                    ▼                    ▼
       ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
       │ElectiveGroupCourse│ │CurriculumElective│  │ (legacy: ölü FK    │
       │ (group ↔ courses)│  │Pool              │  │  CurriculumCourse  │
       │  ElectiveGroupId │  │  CurriculumId    │  │  D1'de DROP)      │
       │  CourseId        │  │  ElectiveGroupId │  └──────────────────┘
       └──────────────────┘  │  SlotName        │
                              │  EctsPerCourse?  │
                              │  RequiredCount   │
                              │  RequiredEcts    │
                              │  Sem.Start/End   │
                              └──────────────────┘
                                       ▲
                                       │ Snapshot FK (kayıt anında)
                                       │
                              ┌──────────────────┐
                              │CourseRegistration│
                              │  AppliedElective │
                              │   PoolId (snapshot)│
                              │  EctsSnapshot    │
                              └──────────────────┘
```

### Scope ladder (3 seviye)
- **Okul** — `FacultyId=null, DepartmentId=null` (OC için)
- **Fakülte** — `FacultyId=set, DepartmentId=null` (MUH-TE-I)
- **Bölüm** — `FacultyId=set, DepartmentId=set` (BLG-TE-I)

Validation: havuz scope'u müfredatın hiyerarşisinde eşit veya daha geniş olmalı.

## Backend Routes

### ElectiveSubtypeController — `/api/elective-subtypes`
```
GET    /                  → List(includeInactive?)         [Authorize] multi-role (Patch 2A)
POST   /                  → Create                          [RequirePermission menu.secmeli-tipleri.create] SysAdmin
PUT    /{id}              → Update (Code immutable)         [RequirePermission menu.secmeli-tipleri.edit] SysAdmin
```
Soft delete: Update IsActive=false. Hard delete YOK.

### ElectiveGroupController — `/api/elective-groups`
```
GET    /                  → List(subtypeId?, facultyId?, departmentId?, isActive?)
                            [RequirePermission menu.secmeli-ders-gruplari.view]
                            Faz E: sadece DB-driven subtypeId (deprecated subtype enum kalktı)
GET    /{id}              → Get
POST   /                  → Create — ResolveSubtypeAsync(int) + ValidateScopeAgainstSubtype
PUT    /{id}              → Update (Code+subtype immutable; scope clear flag'li)
DELETE /{id}              → soft delete (IsActive=false)
GET    /{id}/courses      → ListCourses
POST   /{id}/courses      → AddCourses
DELETE /{id}/courses/{cid}→ RemoveCourse
```

### CurriculumController (pool endpoint'leri)
```
GET    /api/curriculum/{id}/elective-pools                  → ListPools
POST   /api/curriculum/{id}/elective-pools                  → CreatePool (scope ladder + slot uniqueness)
PUT    /api/curriculum/{id}/elective-pools/{poolId}         → UpdatePool (slotName + ectsPerCourse + clearEctsPerCourse)
DELETE /api/curriculum/{id}/elective-pools/{poolId}         → DeletePool
GET    /api/curriculum/student/{studentId}/pool-status      → GetPoolStatus → PoolProgressDto[]
```
`PoolProgressDto`: PoolId, PoolCode, PoolName, Subtype, **SlotName** (C5),
**SuggestedSemesterStart/End** (C4), Required/Completed Course/Ects, EctsPerCourse, IsFulfilled.

### CourseController (öğrenci catalog)
```
GET    /api/course/student-catalog                          → GetStudentCatalog → StudentCourseDto[]
```
`StudentCourseDto.ApplicablePoolSlots: List<ApplicablePoolSlotDto>` (C1) —
her ders için müfredattaki aday slot listesi. Çoğul (Karar #5).

## Key Files

### Domain
- [ElectiveGroup.cs](../../SIS.Domain/Entities/ElectiveGroup.cs) — entity, sadece `ElectiveSubtypeId` FK (NOT NULL); deprecated enum kolon Faz E'de DROP
- [ElectiveSubtypeEntity.cs](../../SIS.Domain/Entities/ElectiveSubtypeEntity.cs) — DB-driven tip tablosu, tek kaynak
- [CurriculumElectivePool.cs](../../SIS.Domain/Entities/CurriculumElectivePool.cs) — slot kaydı (SlotName, EctsPerCourse)
- [ElectiveGroupCourse.cs](../../SIS.Domain/Entities/ElectiveGroupCourse.cs) — composite PK (groupId + courseId)
- [CurriculumCourse.cs](../../SIS.Domain/Entities/CurriculumCourse.cs) — D1+D2'de `ElectiveGroupId` + `GroupCode` DROP
- ~~SIS.Domain/Enums/ElectiveSubtype.cs~~ — Faz E'de SİLİNDİ

### Application
- [ElectiveGroupScopeValidator.cs](../../SIS.Application/ElectiveGroups/Services/ElectiveGroupScopeValidator.cs) — scope ladder kuralı
- [CourseListDto.cs](../../SIS.Application/Common/DTOs/CourseListDto.cs) — StudentCourseDto + ApplicablePoolSlotDto

### Infrastructure
- [AppDbContext.cs](../../SIS.Infrastructure/Persistence/AppDbContext.cs) — ElectiveGroup + ElectiveSubtypeEntity config
- [ElectiveSubtypeSeed.cs](../../SIS.Infrastructure/Persistence/Seeds/ElectiveSubtypeSeed.cs) — TE/NDE/OC default seed (idempotent merge)
- [MenuKatalogu.cs](../../SIS.Infrastructure/Persistence/Katalog/MenuKatalogu.cs) — `Ders İşlemleri > Seçmeli Tipleri` (B3c)
- Migration'lar: `20260427103938_AddElectiveGroupDepartmentId`, `20260427142243_AddCurriculumElectivePoolSlotName`, `20260427144934_AddElectiveSubtypesTable` (+ backfill enum→ID), `20260427200247_DropCurriculumCourseElectiveGroupId`, `20260427200835_DropCurriculumCourseGroupCode`, `20260427205823_DropElectiveGroupElectiveSubtypeColumn` (Faz E)

### API
- [ElectiveSubtypeController.cs](../../SIS.API/Controllers/ElectiveSubtypeController.cs) — DB-driven tip CRUD
- [ElectiveGroupController.cs](../../SIS.API/Controllers/ElectiveGroupController.cs) — group CRUD + scope validation + flag-based dynamic
- [CurriculumController.cs](../../SIS.API/Controllers/CurriculumController.cs) — pool endpoint'leri (Lines ~785-960)
- [CourseController.cs](../../SIS.API/Controllers/CourseController.cs) — student-catalog (Lines ~133-365)

### Frontend (admin)
- [ElectiveSubtypeManagementPage.tsx](../../SIS.Web/src/pages/ElectiveSubtypeManagementPage.tsx) — `/secmeli-yapilandirma/tipler` yeni sayfa
- [ElectiveSubtypeFormDialog.tsx](../../SIS.Web/src/components/elective-subtype/ElectiveSubtypeFormDialog.tsx) — create/edit ortak form (Code lock + flag Switch grid)
- [ElectiveGroupManagementPage.tsx](../../SIS.Web/src/pages/ElectiveGroupManagementPage.tsx) — `/secmeli-yapilandirma/gruplar` (B3c-2 + B3c-3 DB-driven dropdown'lar)
- [CurriculumManagementPage.tsx](../../SIS.Web/src/pages/CurriculumManagementPage.tsx) — `/mufredat-islemleri` (B4 AddPoolDialog + B5 PoolDetailDialog mount)
- [CurriculumCoursesView.tsx](../../SIS.Web/src/pages/curriculum-management/CurriculumCoursesView.tsx) — B5 mixed row tablo (course + pool karışık)
- [AddPoolDialog.tsx](../../SIS.Web/src/components/curriculum-management/AddPoolDialog.tsx) — slot ekleme (SlotName + EctsPerCourse)
- [PoolDetailDialog.tsx](../../SIS.Web/src/components/curriculum-management/PoolDetailDialog.tsx) — inline edit + aday liste + sil onayı (AlertDialog)

### Frontend (öğrenci)
- [CourseCatalogPage.tsx](../../SIS.Web/src/pages/CourseCatalogPage.tsx) — `/ders-kayit` (C2 Müfredat Slotlarım + C4 üstten toggle + slot aday dialog)
- [RegistrationPlanPage.tsx](../../SIS.Web/src/pages/RegistrationPlanPage.tsx) — `/ders-kayit-plani` (C3 eksik slot AlertDialog)
- [CurriculumStatusPage.tsx](../../SIS.Web/src/pages/CurriculumStatusPage.tsx) — `/mufredat-durum` (C5 SlotName progress)

### Frontend types
- [electiveGroup.ts](../../SIS.Web/src/types/electiveGroup.ts) — DTO + Create/Update (sadece electiveSubtypeId — Faz E enum/helper'ları kaldırıldı)
- [electiveSubtype.ts](../../SIS.Web/src/types/electiveSubtype.ts) — tip DTO + Create/Update
- [curriculumManagement.ts](../../SIS.Web/src/types/curriculumManagement.ts) — `CurriculumRow` union (course/pool), `CurriculumElectivePoolDto` (slotName + ectsPerCourse + electiveSubtypeId)
- [course.ts](../../SIS.Web/src/types/course.ts) — `ApplicablePoolSlotDto`, `StudentCourseDto.applicablePoolSlots`
- [lib/electiveSubtypeUtils.ts](../../SIS.Web/src/lib/electiveSubtypeUtils.ts) — Faz E: `getSubtypeColorByCode` + `findSubtypeById` (ortak display helper)

## Veri akışı: Öğrenci ders kayıt

```
1. Öğrenci /ders-kayit'a girer
   ↓
2. CourseCatalogPage:
   - useStudentCatalog()  → StudentCourseDto[] (her course .applicablePoolSlots)
   - useStudentPoolStatus(studentId) → PoolProgressDto[] (slot fulfillment)
   ↓
3. "Müfredat Slotlarım" kartları render — slot tıklayınca dialog
   ↓
4. Dialog: o slot'a aday dersler (filter: c.applicablePoolSlots.some(s => s.poolId === selectedPool.poolId))
   ↓
5. Öğrenci ders ekler → addToPlan mutation → CourseRegistration (Status=Draft)
   ↓
6. /ders-kayit-plani'da submit
   ↓
7. RegistrationPlanPage:
   - submit öncesi useStudentPoolStatus → emptySlots filter
   - emptySlots > 0 ise AlertDialog "Eksik müfredat slotları var" → "Yine de gönder"
   ↓
8. Backend submit:
   - ResolveEctsSnapshotAsync → AppliedElectivePoolId snapshot atılır
   - EctsSnapshot = pool.EctsPerCourse ?? course.Ects
```

## Veri akışı: Admin müfredata grup ekleme

```
1. /mufredat-islemleri → müfredat seçer → "Ders Listesi" tab'a
   ↓
2. CurriculumCoursesView'de "+ Grup Ekle" buton
   ↓
3. AddPoolDialog açılır:
   - useElectiveGroupList(undefined, ...) → tüm aktif gruplar dropdown
   - Form: ElectiveGroup + SlotName + Semester[Start-End] + Required[Count, Ects] + EctsPerCourse?
   ↓
4. POST /api/curriculum/{id}/elective-pools (CreatePool)
   - scope validator: group.scope ≤ curriculum.scope
   - slotName uniqueness: (curriculumId, groupId, slotName)
   ↓
5. CurriculumElectivePool kaydı oluşur → tablo refresh
   ↓
6. Pool satırı tablo'da görünür (violet renkli, "Grup" badge)
   ↓
7. Tıklayınca PoolDetailDialog → inline edit / sil onayı
```

## 14 Davranış Kararı (özet)

| # | Konu | Karar |
|---|------|-------|
| 1 | Submit gate | Yumuşak warning, danışman süzer |
| 2 | Üstten seçmeli alma | Mevcut "üstten ders" mantığı otomatik |
| 3 | Aynı ders iki slot dolduramaz | UI filter |
| 4 | NDE scope | Default okul-geneli, esnek |
| 5 | Aynı ders birden fazla grupta | İzin var |
| 6 | AKTS override | Pool seviyesinde (`EctsPerCourse`) |
| 7 | Cross-listed dersler | Temsilci yöntemi (CourseCrossList) |
| 8 | Müfredat görünümü | Proliz tarzı karışık satırlar |
| 9 | Published müfredat + gruba yeni ders | Esnek, eski öğrenciler de görür |
| 10 | FF tekrarı + grup | Gruptan başka ders seçilebilir |
| 11 | Yaz okulu grupları | Aynı grup, yaz dahil |
| 12 | Grup scope sonradan değişimi | Sistem uyarır, admin manuel temizler |
| 13 | Grup oluşturma yetkisi | SysAdmin + Dean + Bölüm Başkanı |
| 14 | Tip adı değişimi | Code immutable, sadece Name editable |

## Faz Tarihçesi

| Faz | Commits | Özet |
|-----|---------|------|
| **A** | 10 commit (`d38279c..5daf4f1`) | DepartmentId scope ladder + validator + admin sayfa |
| **B** | 11 commit (B1-B6 + B3c-3) | Pool/slot model + DB-driven subtype + UI tam |
| **C** | 5 commit (C1-C5) | Öğrenci slot UI + warning gate + üstten toggle |
| **D** | 3 commit (D1-D3) + 1 codemap | Backend cleanup (ölü kolonlar drop) + dokümantasyon |
| **E** | 2 commit (E1+E2) + 1 doc | Deprecated `ElectiveSubtype` enum kolon + dosya + DTO/display tam temizlik |

**Refactor closed** — Faz 6 tamamlandı, gelecek faz YOK.
