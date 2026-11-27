# RBAC Phase 1 — Deployment Checklist

> **Tag:** `rbac-phase1-mvp`
> **Migration:** `20260425132603_AddRBACPermissions` (5 yeni tablo, mevcut tablolara dokunmaz)
> **Risk:** Düşük (additive migration, idempotent seed, rollback mümkün)

## 1. Pre-Deployment

### 1.1 Backup (zorunlu)

```bash
# Production DB
pg_dump -U <prod_user> -h <prod_host> -p 5432 <prod_db> > backup_pre_rbac_$(date +%Y%m%d_%H%M%S).sql
```

Backup dosya boyutunu doğrula (boş olmamalı). Güvenli bir konuma yükle (offsite).

### 1.2 Build & Test

```bash
# Local CI hattında
dotnet build SIS.API/SIS.API.csproj --configuration Release
dotnet test SIS.Tests/SIS.Tests.csproj --filter "FullyQualifiedName~PermissionServiceTests|FullyQualifiedName~MenuServiceTests|FullyQualifiedName~RequirePermissionAttributeTests"

# Frontend
cd SIS.Web && npm run build
```

Beklenen: 0 hata, 17 unit test geçmeli, vite build başarılı.

### 1.3 Mevcut DB durumunu kaydet

```sql
SELECT 'AspNetRoles' AS tbl, COUNT(*) FROM "AspNetRoles"
UNION ALL SELECT 'AspNetUsers', COUNT(*) FROM "AspNetUsers"
UNION ALL SELECT 'AspNetUserRoles', COUNT(*) FROM "AspNetUserRoles";
```

Bu sayılar deploy sonrası **değişmemeli** (RBAC mevcut Identity tablolarına dokunmaz).

## 2. Deployment

### 2.1 Migration Apply

```bash
dotnet ef database update -p SIS.Infrastructure -s SIS.API
```

Beklenen çıktı:
```
Applying migration '20260425132603_AddRBACPermissions'.
Done.
```

### 2.2 Schema verification

```sql
SELECT table_name FROM information_schema.tables
WHERE table_schema='public' AND table_name IN
  ('Permissions','RolePermissions','MenuModules','MenuItems','MenuItemPermissions')
ORDER BY table_name;
```

Beklenen: **5 satır** dönmeli.

### 2.3 Application restart

API process'i restart edilir. İlk açılışta `SeedData.SeedAsync` orchestrator otomatik çağrılır → 3 RBAC seed sırayla çalışır:
1. Sistem kataloğu (`SistemKatalogu.EsitleAsync`) — roller ve izinler; eksik olanı ekler (~255 key)
2. Menü kataloğu (`MenuKatalogu.EsitleAsync`) — 18 modül + 74 öğe; eksik olanı ekler, yöneticinin sildiğini geri getirmez
3. Rol-izin kataloğu (`RolIzinKatalogu.EsitleAsync`) — 432 atama, 6 rol; eksik olanı ekler, kaldırılanı geri getirmez

**Not:** Mevcut `SeedData` zinciri büyük (Faculties, Students, Courses, vb. seed'leri); production'da bu seed'ler genelde development-only ile koşullu çalışır (Program.cs `app.Environment.IsDevelopment()` check). Production deploy sonrası RBAC seed'leri **manuel script** ile çalıştırmak gerekebilir — `dotnet ef`'in seed entegrasyonu yok, manuel `dotnet run --seed-only` veya admin endpoint ile tetiklenir.

## 3. Post-Deployment Verification

### 3.1 Seed sonuç doğrulama

```sql
SELECT 'Permissions' AS tbl, COUNT(*) FROM "Permissions"
UNION ALL SELECT 'MenuModules', COUNT(*) FROM "MenuModules"
UNION ALL SELECT 'MenuItems', COUNT(*) FROM "MenuItems"
UNION ALL SELECT 'RolePermissions', COUNT(*) FROM "RolePermissions"
UNION ALL SELECT 'MenuItemPermissions', COUNT(*) FROM "MenuItemPermissions";
```

Beklenen aralıklar (production'da rol/permission ayarlarına göre değişebilir):

| Tablo | Beklenen sayı |
|-------|---------------|
| Permissions | ~239 |
| MenuModules | 13 |
| MenuItems | 75 |
| RolePermissions | ~360-460 (rol×permission union) |
| MenuItemPermissions | 75 |

### 3.2 Smoke test (curl)

```bash
# SystemAdmin login
TOKEN=$(curl -s -X POST https://<api>/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"<sysadmin_email>","password":"<sysadmin_password>"}' \
  | jq -r '.token')

# Permissions endpoint
curl -s https://<api>/api/me/permissions -H "Authorization: Bearer $TOKEN" | jq 'length'
# Beklenen: 239 (SystemAdmin tüm DB key'lerini görür)

# Menu endpoint
curl -s https://<api>/api/me/menu -H "Authorization: Bearer $TOKEN" | jq '. | length'
# Beklenen: 10-11 top-level modül
```

### 3.3 Permission enforcement testi

Permission'ı OLMAYAN bir rol ile yetkili endpoint'e istek atıp 403 dönmesini doğrula:

```bash
# Dean login (Dean'a menu.akademik-donem.view default seed'de verilmedi)
DEAN_TOKEN=$(curl -s -X POST https://<api>/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"<dean_email>","password":"<password>"}' | jq -r '.token')

curl -s -o /dev/null -w "HTTP %{http_code}\n" \
  https://<api>/api/admin/terms \
  -H "Authorization: Bearer $DEAN_TOKEN"
# Beklenen: HTTP 403
```

### 3.4 Frontend smoke (browser)

- SystemAdmin login → sidebar'da 10 modül + Yönetim → 2 nested görünmeli
- Permission değişikliği testi: SysAdmin admin matrisinde bir rolün permission'ını değiştir → o rolden bir kullanıcı login olunca yeni permission set'ini görmeli (max 5 dk içinde)

### 3.5 Orphan detection

```bash
bash scripts/check-permission-orphans.sh
```

Beklenen: **0 eksik DB key** (sadece 5 lookup.* — DB seed beklenmez, hardcoded shortcut OK).

## 4. Rollback Senaryosu

### 4.1 Kod rollback

```bash
git checkout <previous_commit_before_rbac>
# Restart API
```

API yeni RBAC kodunu kaldırır, eski `[Authorize(Roles=...)]` davranışına döner. **Ancak** mevcut DB'de RBAC tabloları kalır (boş migrate edildi, sorun değil).

### 4.2 Tam DB rollback (gerekirse)

```bash
# Migration rollback
dotnet ef database update <previous_migration> -p SIS.Infrastructure -s SIS.API
# 5 RBAC tablosu DROP edilir, mevcut Identity tabloları korunur (FK CASCADE değil)
```

### 4.3 Backup'tan restore (acil)

```bash
psql -U <user> -h <host> -d <db> < backup_pre_rbac_<timestamp>.sql
```

Bu, deployment öncesi tam state'e döner. **Veri kaybı:** RBAC sonrası deploy ile arasındaki tüm değişiklikler.

## 5. Bilinen Sınırlamalar (Phase 1 MVP)

1. **Permission değişikliği latency:** Max 5 dk (cache TTL). Real-time istiyorsan Phase 2'de WebSocket veya SSE ile push notification eklenebilir.
2. **Audit log YOK** (Karar 10): Permission değişikliklerini izlemek için `AuditLog` tablosu var ama şu an permission action'ları log'lanmıyor. Phase 2-6'da eklenir.
3. **Admin UI sidebar'a eklenmedi** (Karar 9): `/__dev/permission-matrix` dev-only. Phase 2'de "Kullanıcı Yetki Grupları" placeholder içine import edilecek.
4. **`lookup.*` permission'ları admin tarafından kısıtlanamaz** (Karar 7): Hardcoded shortcut. Admin lookup erişimini kısıtlamak isterse Phase 5'te bu shortcut kaldırılır + DB seed'e eklenir.

## 6. Deployment Sonrası Checklist

- [ ] Backup başarılı, dosya boyutu doğru
- [ ] Migration apply 0 hata
- [ ] 5 RBAC tablosu var
- [ ] Seed sonuç sayıları beklenen aralıkta
- [ ] SystemAdmin smoke test 200
- [ ] Yetki yetersiz rol enforcement test 403
- [ ] Frontend SystemAdmin login sidebar render OK
- [ ] Orphan script 0 eksik DB key
- [ ] Mevcut Identity tabloları (AspNetUsers/Roles/UserRoles) sayıları değişmedi
- [ ] Production logs'ta unhandled exception yok (ilk 1 saat)
- [ ] Deploy notu / changelog güncellendi
