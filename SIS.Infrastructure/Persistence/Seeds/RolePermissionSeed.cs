using SIS.Domain.Identity;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// RBAC Phase 1 — 6 default rolün varsayılan izin listesi. Yazma işi rol-izin kataloğunda
/// (Persistence/Katalog/RolIzinKatalogu.cs): eksik atamayı ekler, kaldırılanı geri getirmez.
///
/// Inventory: <c>docs/superpowers/plans/2026-04-25-rbac-default-seed.md</c> (Sprint 0.3 sign-off).
///
/// Önemli kurallar:
///  - <b>SystemAdministrator</b> rolü için seed YOK — Karar 4 gereği kod düzeyinde
///    <c>RequirePermissionAttribute</c> shortcut ile tüm permission'lara erişir.
///  - Her rol için sadece <see cref="RoleDefaults"/>'da listelenen permission key'ler verilir.
///  - <c>lookup.*</c> permission'ları DB'de yok — hardcoded shortcut ile her authenticated user'a açık.
///  - Listede olup veritabanında olmayan rol ya da izin kurulumu durdurur.
///
/// Toplam: <b>432</b> atama.
///
/// Çalıştırma sırası: <see cref="PermissionSeed"/> + IdentityRole seed ÖNCE çalışmalı.
/// </summary>
public static class RolePermissionSeed
{
    /// <summary>
    /// Default rol → permission key listesi mapping'i. SystemAdministrator dahil değildir (locked).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RoleDefaults =
        new Dictionary<string, IReadOnlyList<string>>
        {
            // ═══════════════════════════════════════════════════════════════════════════
            // Student — 26 permission (17 view + 9 action)
            // Sidebar: Sık Kullanılanlar + Ders ve Dönem İşlemleri + Anketler + Hesabım
            // ═══════════════════════════════════════════════════════════════════════════
            ["Student"] = new[]
            {
                // ── View permissions (17) ──
                "menu.ana-sayfa.view",
                "menu.akademik-takvim.view",
                "menu.duyurular.view",
                "menu.alinan-dersler.view",
                "menu.raporlarim.view",
                "menu.danisman-bilgileri.view",
                "menu.acilan-bolum-dersleri.view",
                "menu.bolum-ders-programi.view",
                "menu.bolum-ders-programi.export",
                "menu.ders-kayit.view",
                "menu.ders-programim.view",
                "menu.plan.view",
                "menu.not-listesi.view",
                "menu.devamsizlik-durumu.view",
                "menu.donem-ortalamalari.view",
                "menu.mufredat-durum.view",
                "menu.transkript.view",
                "menu.transkript-senaryosu.view",
                "menu.ders-degerlendirme.view",
                "menu.sifre-degistir.view",

                // ── Action permissions (9) ──
                "menu.ders-kayit.plan",
                "menu.ders-kayit.submit",
                "menu.ders-kayit.drop",
                "menu.ders-kayit.withdraw",
                "menu.alinan-dersler.statistics",
                "menu.ders-degerlendirme.submit",
                "menu.ders-programi-kisitlamalari.exception-request",
                "menu.ders-programi-kisitlamalari.exception-my",
                "menu.ders-programi-kisitlamalari.exception-view",
            },

            // ═══════════════════════════════════════════════════════════════════════════
            // Instructor — 25 permission (9 view + 16 action)
            // Sidebar: Sık Kullanılanlar + Ders İşlemleri (subset) + Anketler + Hesabım
            // ═══════════════════════════════════════════════════════════════════════════
            ["Instructor"] = new[]
            {
                // ── View permissions ──
                "menu.ana-sayfa.view",
                "menu.akademik-takvim.view",
                "menu.duyurular.view",
                "menu.derslerim.view",
                "menu.raporlarim.view",
                "menu.yoklama-girisi.view",
                "menu.ders-programim.view",
                "menu.not-girisi.view",
                "menu.sinav-tanimlama.view",
                "menu.danisman-ders-kisitlari.view",
                "menu.degerlendirme-sonuclari.view",
                "menu.sifre-degistir.view",
                // Patch 2B: "Bölüm İşlemleri" modülü için (Proliz tarzı hoca paneli)
                "menu.subelendirme-islemleri.view",
                "menu.ders-veren-ogretim-uyeleri.view",
                "menu.toplu-danisman-atama.view",
                "menu.toplu-mufredat-atama.view",
                // Hiyerarşik duyuru: hoca kendi ders şubesi öğrencilerine duyuru yayınlayabilir
                "menu.duyuru-yonetimi.view",

                // ── Action permissions ──
                "menu.sinav-tanimlama.manage",
                "menu.not-girisi.edit",
                "menu.not-girisi.submit",
                "menu.not-girisi.export",
                "menu.not-girisi.import",
                "menu.not-girisi.finalize",
                "menu.not-girisi.unfinalize",
                "menu.not-girisi.publish",
                "menu.ders-programim.edit",
                "menu.danisman-ders-kisitlari.create",
                "menu.danisman-ders-kisitlari.edit",
                "menu.danisman-ders-kisitlari.delete",
                "menu.ders-programi-kisitlamalari.exception-request",
                "menu.ders-programi-kisitlamalari.exception-my",
                "menu.ders-programi-kisitlamalari.exception-view",
                // Patch 2A: ders-kayit.view + mufredat-durum.view kaldırıldı.
                // Hiyerarşik duyuru CRUD
                "menu.duyuru-yonetimi.create",
                "menu.duyuru-yonetimi.edit",
                "menu.duyuru-yonetimi.delete",
            },

            // ═══════════════════════════════════════════════════════════════════════════
            // AcademicAdvisor — sadece danışmanlık sayfaları.
            // C2 sign-off: not-girisi.* YOK — sidebar'dan kaldırıldı.
            // Hoca/ders-verme sayfaları (derslerim, ders-programim, yoklama-girisi,
            // degerlendirme-sonuclari) bilinçli olarak çıkarıldı: bunlar Instructor
            // rolüne aittir, saf danışman ders vermez. Aynı kişi hem hoca hem danışman
            // ise bu sayfaları Öğretim Üyesi rolüyle görür.
            // ═══════════════════════════════════════════════════════════════════════════
            ["AcademicAdvisor"] = new[]
            {
                // ── View permissions ──
                "menu.ana-sayfa.view",
                "menu.akademik-takvim.view",
                "menu.raporlarim.view",
                "menu.duyurular.view",
                "menu.danisman-ders-kisitlari.view",
                "menu.ders-kayit-onay.view",
                "menu.ogrenci-rehberi.view",
                "menu.sifre-degistir.view",

                // ── Action permissions (21) ──
                "menu.ders-kayit-onay.approve",
                "menu.ders-kayit-onay.reject",
                "menu.danisman-ders-kisitlari.create",
                "menu.danisman-ders-kisitlari.edit",
                "menu.danisman-ders-kisitlari.delete",
                "menu.ders-programi-kisitlamalari.exception-request",
                "menu.ders-programi-kisitlamalari.exception-my",
                "menu.ders-programi-kisitlamalari.exception-view",
                // Patch 2A: ders-kayit.view + mufredat-durum.view kaldırıldı.
                // Hiyerarşik duyuru: danışman öğrencilere duyuru yayınlayabilir
                "menu.duyuru-yonetimi.view",
                "menu.duyuru-yonetimi.create",
                "menu.duyuru-yonetimi.edit",
                "menu.duyuru-yonetimi.delete",
            },

            // ═══════════════════════════════════════════════════════════════════════════
            // AdministrativeStaff — 110 permission (19 view + 91 action)
            // ═══════════════════════════════════════════════════════════════════════════
            ["AdministrativeStaff"] = new[]
            {
                // ── View permissions ──
                "menu.ana-sayfa.view",
                "menu.raporlarim.view",
                // Patch: Duyurular admin'lerden kaldırıldı (Genel İşlemler
                // modülü öğrenci-merkezli). Admin için Duyurular Duyuru Yönetimi altında.
                "menu.akademik-takvim.view",
                "menu.duyurular-admin.view",
                "menu.akademik-takvim-olusturma.view",
                "menu.ders-katalogu.view",
                "menu.mufredat-islemleri.view",
                "menu.secmeli-yapilandirma.view",
                "menu.secmeli-ders-gruplari.view",
                "menu.ders-acma-islemleri.view",
                "menu.sinav-mufredati.view",
                "menu.sinav-mufredati.edit",
                "menu.sinav-mufredati.create",
                "menu.sinav-mufredati.delete",
                "menu.mufredat-ders-intibak.view",
                "menu.mufredat-ders-intibak.edit",
                "menu.ders-programi-olusturma.view",
                "menu.ogretim-elemani-atama.view",
                "menu.donem-programi-aktarim.view",
                "menu.derslik-doluluk.view",
                "menu.subelendirme-islemleri.view",
                "menu.subelendirme-islemleri.transfer",
                "menu.subelendirme-islemleri.auto-split",
                "menu.ders-veren-ogretim-uyeleri.view",
                "menu.toplu-danisman-atama.view",
                "menu.toplu-mufredat-atama.view",
                "menu.ders-programi-kisitlamalari.view",
                "menu.ogrenci-rehberi.view",
                "menu.duyuru-yonetimi.view",
                "menu.sifre-degistir.view",
                "menu.degerlendirme-sonuclari.view",
                "menu.personel-listesi.view",

                // ── Action permissions: Akademik Takvim Oluşturucu (broad) ──
                "menu.akademik-takvim-olusturma.generate",
                "menu.akademik-takvim-olusturma.simulate",

                // ── Ders Kataloğu (CRUD — Öğr. İşleri kataloğu yönetir) ──
                "menu.ders-katalogu.create",
                "menu.ders-katalogu.edit",
                "menu.ders-katalogu.delete",
                "menu.ders-katalogu.prerequisite",
                "menu.ders-katalogu.equivalency",
                "menu.ders-katalogu.cross-list",

                // ── Müfredat (publish/archive HARİÇ — DepMgr+) ──
                "menu.mufredat-islemleri.create",
                "menu.mufredat-islemleri.edit",
                "menu.mufredat-islemleri.delete",
                "menu.mufredat-islemleri.add-course",
                "menu.mufredat-islemleri.edit-course",
                "menu.mufredat-islemleri.remove-course",
                "menu.mufredat-islemleri.copy",
                "menu.mufredat-islemleri.activate",
                "menu.mufredat-islemleri.create-pool",
                "menu.mufredat-islemleri.edit-pool",
                "menu.mufredat-islemleri.delete-pool",
                "menu.mufredat-sinif-donem.view",
                "menu.mufredat-sinif-donem.create",
                "menu.mufredat-sinif-donem.edit",
                "menu.mufredat-sinif-donem.delete",

                // ── Seçmeli Gruplar ──
                "menu.secmeli-ders-gruplari.create",
                "menu.secmeli-ders-gruplari.edit",
                "menu.secmeli-ders-gruplari.delete",
                "menu.secmeli-ders-gruplari.add-course",
                "menu.secmeli-ders-gruplari.remove-course",

                // ── Ders Açma ──
                "menu.ders-acma-islemleri.open",
                "menu.ders-acma-islemleri.bulk-open",
                "menu.ders-acma-islemleri.edit",
                "menu.ders-acma-islemleri.close-section",
                "menu.ders-acma-islemleri.close-all",
                "menu.ders-acma-islemleri.reactivate",
                "menu.ogretim-elemani-atama.bulk-assign",

                // ── Programlama (swap/lock/unlock HARİÇ — DepMgr+) ──
                "menu.ders-programi-olusturma.assign",
                "menu.ders-programi-olusturma.unassign",
                "menu.ders-programi-olusturma.reset-all",
                "menu.ders-programi-olusturma.auto-schedule",
                "menu.ders-programi-olusturma.apply",
                "menu.ders-programi-olusturma.validate",
                "menu.ders-programi-olusturma.suggest",
                "menu.ders-programi-olusturma.export",
                "menu.ders-programi-olusturma.export-pdf",
                "menu.ders-programi-olusturma.import",

                // ── Kısıtlamalar (toggle/weight HARİÇ — SysAdmin only; exception HARİÇ — DepMgr+) ──
                "menu.ders-programi-kisitlamalari.create",
                "menu.ders-programi-kisitlamalari.edit",
                "menu.ders-programi-kisitlamalari.delete",
                "menu.ders-programi-kisitlamalari.simulate",

                // ── Dönem Programı Aktarım ──
                "menu.donem-programi-aktarim.transfer",

                // ── Derslik Export ──
                "menu.derslik-doluluk.export",

                // ── Öğrenci & Personel CRUD ──
                "menu.ogrenci-rehberi.create",
                "menu.ogrenci-rehberi.edit-academic", // edit-academic: şimdilik yalnızca AdministrativeStaff — akademik-düzenleme planında Dean/DepartmentManager kapsamı tekrar değerlendirilecek
                "menu.ogrenci-rehberi.edit-personal",
                "menu.ogrenci-rehberi.edit-documents",
                "menu.ogrenci-rehberi.generate-documents",
                "menu.ogrenci-rehberi.edit-scholarship",
                "menu.ogrenci-rehberi.edit-placement",
                "menu.ogrenci-rehberi.integration",
                "menu.ogrenci-no-degistirme.change",

                // ── Sınıf Atlatma (öğrenci kartı paneli) ──
                "menu.sinif-atlatma-islemleri.view",
                "menu.sinif-atlatma-islemleri.promote",
                "menu.sinif-atlatma-islemleri.rollback",
                "menu.toplu-danisman-atama.assign",
                "menu.toplu-mufredat-atama.assign",
                "menu.personel-listesi.create",

                // ── Duyuru Yönetimi CRUD ──
                "menu.duyuru-yonetimi.create",
                "menu.duyuru-yonetimi.edit",
                "menu.duyuru-yonetimi.delete",

                // ── Cross-cutting (authenticated) ──
                // Patch 2A: ders-kayit.view + mufredat-durum.view kaldırıldı (sidebar Ders ve Dönem
                // sadece Student'ta gözükür; backend Course/Eligibility/Curriculum permission'sız).
                // ders-degerlendirme.view Student-only (2026-05-02).
                "menu.ders-programi-kisitlamalari.exception-request",
                "menu.ders-programi-kisitlamalari.exception-my",
                "menu.ders-programi-kisitlamalari.exception-view",

                // ── Cross-cutting (broad role list) ──
                "menu.akilli-planlama.view",
                "menu.akilli-planlama.forecast",
                "menu.akilli-planlama.simulate",
            },

            // ═══════════════════════════════════════════════════════════════════════════
            // DepartmentManager — 125 permission (25 view + 100 action)
            // AdministrativeStaff'ın yetkileri + onay ekleri (publish/archive, swap/lock/unlock,
            // exception approve, dönem kapatma, sınıf atlatma)
            // ═══════════════════════════════════════════════════════════════════════════
            ["DepartmentManager"] = new[]
            {
                // ── View permissions ──
                "menu.ana-sayfa.view",
                "menu.raporlarim.view",
                // Patch: duyurular admin'lerden kaldırıldı.
                "menu.akademik-takvim.view",
                "menu.duyurular-admin.view",
                "menu.ders-katalogu.view",
                "menu.mufredat-islemleri.view",
                "menu.secmeli-yapilandirma.view",
                "menu.secmeli-ders-gruplari.view",
                "menu.ders-acma-islemleri.view",
                "menu.sinav-mufredati.view",
                "menu.sinav-mufredati.edit",
                "menu.sinav-mufredati.create",
                "menu.sinav-mufredati.delete",
                "menu.sinav-tanimlama.view",
                "menu.sinav-tanimlama.manage",
                "menu.mufredat-ders-intibak.view",
                "menu.mufredat-ders-intibak.edit",
                "menu.ders-programi-kisitlamalari.view",
                "menu.ogretim-elemani-atama.view",
                "menu.donem-programi-aktarim.view",
                "menu.subelendirme-islemleri.view",
                "menu.subelendirme-islemleri.transfer",
                "menu.subelendirme-islemleri.auto-split",
                "menu.ders-veren-ogretim-uyeleri.view",
                "menu.donem-kapatma.view",
                "menu.derslerim.view",
                "menu.yoklama-girisi.view",
                "menu.ders-programim.view",
                "menu.not-girisi.view",
                "menu.ders-programi-olusturma.view",
                "menu.derslik-doluluk.view",
                "menu.degerlendirme-sonuclari.view",
                "menu.ogrenci-rehberi.view",
                "menu.duyuru-yonetimi.view",
                "menu.personel-listesi.view",
                "menu.sifre-degistir.view",
                "menu.sinif-atlatma-islemleri.view",
                "menu.toplu-danisman-atama.view",
                "menu.toplu-mufredat-atama.view",
                "menu.mufredat-sinif-donem.view",

                // ── Ders Kataloğu (CRUD) ──
                "menu.ders-katalogu.create",
                "menu.ders-katalogu.edit",
                "menu.ders-katalogu.delete",
                "menu.ders-katalogu.prerequisite",
                "menu.ders-katalogu.equivalency",
                "menu.ders-katalogu.cross-list",

                // ── Sınıf Atlatma Kuralları (promotion rules — görüntüle + düzenle) ──
                "menu.sinif-atlatma-kurallari.view",
                "menu.sinif-atlatma-kurallari.create",
                "menu.sinif-atlatma-kurallari.edit",
                "menu.sinif-atlatma-kurallari.delete",

                // ── Müfredat (publish/archive DAHIL) ──
                "menu.mufredat-islemleri.create",
                "menu.mufredat-islemleri.edit",
                "menu.mufredat-islemleri.delete",
                "menu.mufredat-islemleri.add-course",
                "menu.mufredat-islemleri.edit-course",
                "menu.mufredat-islemleri.remove-course",
                "menu.mufredat-islemleri.copy",
                "menu.mufredat-islemleri.activate",
                "menu.mufredat-islemleri.create-pool",
                "menu.mufredat-islemleri.edit-pool",
                "menu.mufredat-islemleri.delete-pool",
                "menu.mufredat-islemleri.publish",
                "menu.mufredat-islemleri.archive",
                "menu.mufredat-sinif-donem.create",
                "menu.mufredat-sinif-donem.edit",
                "menu.mufredat-sinif-donem.delete",

                // ── Seçmeli Gruplar ──
                "menu.secmeli-ders-gruplari.create",
                "menu.secmeli-ders-gruplari.edit",
                "menu.secmeli-ders-gruplari.delete",
                "menu.secmeli-ders-gruplari.add-course",
                "menu.secmeli-ders-gruplari.remove-course",

                // ── Ders Açma ──
                "menu.ders-acma-islemleri.open",
                "menu.ders-acma-islemleri.bulk-open",
                "menu.ders-acma-islemleri.edit",
                "menu.ders-acma-islemleri.close-section",
                "menu.ders-acma-islemleri.close-all",
                "menu.ders-acma-islemleri.reactivate",
                "menu.ogretim-elemani-atama.bulk-assign",

                // ── Programlama (swap/lock/unlock DAHIL) ──
                "menu.ders-programi-olusturma.assign",
                "menu.ders-programi-olusturma.unassign",
                "menu.ders-programi-olusturma.reset-all",
                "menu.ders-programi-olusturma.auto-schedule",
                "menu.ders-programi-olusturma.apply",
                "menu.ders-programi-olusturma.validate",
                "menu.ders-programi-olusturma.suggest",
                "menu.ders-programi-olusturma.export",
                "menu.ders-programi-olusturma.export-pdf",
                "menu.ders-programi-olusturma.import",
                "menu.ders-programi-olusturma.swap",
                "menu.ders-programi-olusturma.lock",
                "menu.ders-programi-olusturma.unlock",

                // ── Kısıtlamalar (exception DAHIL) ──
                "menu.ders-programi-kisitlamalari.create",
                "menu.ders-programi-kisitlamalari.edit",
                "menu.ders-programi-kisitlamalari.delete",
                "menu.ders-programi-kisitlamalari.simulate",
                "menu.ders-programi-kisitlamalari.exception",
                "menu.ders-programi-kisitlamalari.exception-pending",
                "menu.ders-programi-kisitlamalari.exception-approve",
                "menu.ders-programi-kisitlamalari.exception-reject",

                // ── Dönem Programı Aktarım ──
                "menu.donem-programi-aktarim.transfer",

                // ── Sınıf Atlatma ──
                "menu.sinif-atlatma-islemleri.promote",
                "menu.sinif-atlatma-islemleri.rollback",
                "menu.sinif-atlatma-islemleri.export",

                // ── Derslik Export ──
                "menu.derslik-doluluk.export",

                // ── Öğrenci ──
                "menu.toplu-danisman-atama.assign",
                "menu.toplu-mufredat-atama.assign",

                // ── Duyuru Yönetimi CRUD ──
                "menu.duyuru-yonetimi.create",
                "menu.duyuru-yonetimi.edit",
                "menu.duyuru-yonetimi.delete",

                // ── Duyuru Kategorileri CRUD ──
                "menu.duyuru-kategorileri.view",
                "menu.duyuru-kategorileri.create",
                "menu.duyuru-kategorileri.edit",
                "menu.duyuru-kategorileri.delete",

                // ── Cross-cutting (authenticated) ──
                // Patch 2A: ders-kayit.view + mufredat-durum.view kaldırıldı.
                // ders-degerlendirme.view Student-only (2026-05-02).
                "menu.ders-programi-kisitlamalari.exception-request",
                "menu.ders-programi-kisitlamalari.exception-my",
                "menu.ders-programi-kisitlamalari.exception-view",

                // ── Cross-cutting (broad role list) ──
                "menu.akilli-planlama.view",
                "menu.akilli-planlama.forecast",
                "menu.akilli-planlama.simulate",
            },

            // ═══════════════════════════════════════════════════════════════════════════
            // Dean — DepartmentManager + faculty-level program onayı + AdminStaff CRUD
            // ═══════════════════════════════════════════════════════════════════════════
            ["Dean"] = new[]
            {
                // ── View permissions ──
                "menu.ana-sayfa.view",
                "menu.raporlarim.view",
                // Patch: duyurular admin'lerden kaldırıldı.
                "menu.akademik-takvim.view",
                "menu.duyurular-admin.view",
                "menu.ders-katalogu.view",
                "menu.mufredat-islemleri.view",
                "menu.secmeli-yapilandirma.view",
                "menu.secmeli-ders-gruplari.view",
                "menu.ders-acma-islemleri.view",
                "menu.sinav-mufredati.view",
                "menu.sinav-mufredati.edit",
                "menu.sinav-mufredati.create",
                "menu.sinav-mufredati.delete",
                "menu.sinav-tanimlama.view",
                "menu.sinav-tanimlama.manage",
                "menu.mufredat-ders-intibak.view",
                "menu.mufredat-ders-intibak.edit",
                "menu.ders-programi-kisitlamalari.view",
                "menu.ders-programi-olusturma.view",
                "menu.ogretim-elemani-atama.view",
                "menu.donem-programi-aktarim.view",
                "menu.derslik-doluluk.view",
                "menu.subelendirme-islemleri.view",
                "menu.subelendirme-islemleri.transfer",
                "menu.subelendirme-islemleri.auto-split",
                "menu.ders-veren-ogretim-uyeleri.view",
                "menu.program-onayi.view",
                "menu.donem-kapatma.view",
                "menu.derslerim.view",
                "menu.yoklama-girisi.view",
                "menu.ders-programim.view",
                "menu.not-girisi.view",
                "menu.degerlendirme-sonuclari.view",
                "menu.ogrenci-rehberi.view",
                "menu.duyuru-yonetimi.view",
                "menu.personel-listesi.view",
                "menu.sifre-degistir.view",
                "menu.sinif-atlatma-islemleri.view",

                // ── Toplu Atama ──
                "menu.toplu-danisman-atama.view",
                "menu.toplu-mufredat-atama.view",
                "menu.mufredat-sinif-donem.view",

                // ── Ders Kataloğu (CRUD) ──
                "menu.ders-katalogu.create",
                "menu.ders-katalogu.edit",
                "menu.ders-katalogu.delete",
                "menu.ders-katalogu.prerequisite",
                "menu.ders-katalogu.equivalency",
                "menu.ders-katalogu.cross-list",

                // ── Sınıf Atlatma Kuralları (promotion rules — görüntüle + düzenle) ──
                "menu.sinif-atlatma-kurallari.view",
                "menu.sinif-atlatma-kurallari.create",
                "menu.sinif-atlatma-kurallari.edit",
                "menu.sinif-atlatma-kurallari.delete",

                // ── Müfredat (publish/archive DAHIL) ──
                "menu.mufredat-islemleri.create",
                "menu.mufredat-islemleri.edit",
                "menu.mufredat-islemleri.delete",
                "menu.mufredat-islemleri.add-course",
                "menu.mufredat-islemleri.edit-course",
                "menu.mufredat-islemleri.remove-course",
                "menu.mufredat-islemleri.copy",
                "menu.mufredat-islemleri.activate",
                "menu.mufredat-islemleri.create-pool",
                "menu.mufredat-islemleri.edit-pool",
                "menu.mufredat-islemleri.delete-pool",
                "menu.mufredat-islemleri.publish",
                "menu.mufredat-islemleri.archive",
                "menu.mufredat-sinif-donem.create",
                "menu.mufredat-sinif-donem.edit",
                "menu.mufredat-sinif-donem.delete",

                // ── Seçmeli Gruplar ──
                "menu.secmeli-ders-gruplari.create",
                "menu.secmeli-ders-gruplari.edit",
                "menu.secmeli-ders-gruplari.delete",
                "menu.secmeli-ders-gruplari.add-course",
                "menu.secmeli-ders-gruplari.remove-course",

                // ── Ders Açma ──
                "menu.ders-acma-islemleri.open",
                "menu.ders-acma-islemleri.bulk-open",
                "menu.ders-acma-islemleri.edit",
                "menu.ders-acma-islemleri.close-section",
                "menu.ders-acma-islemleri.close-all",
                "menu.ders-acma-islemleri.reactivate",
                "menu.ogretim-elemani-atama.bulk-assign",

                // ── Programlama (swap/lock/unlock DAHIL) ──
                "menu.ders-programi-olusturma.assign",
                "menu.ders-programi-olusturma.unassign",
                "menu.ders-programi-olusturma.reset-all",
                "menu.ders-programi-olusturma.auto-schedule",
                "menu.ders-programi-olusturma.apply",
                "menu.ders-programi-olusturma.validate",
                "menu.ders-programi-olusturma.suggest",
                "menu.ders-programi-olusturma.export",
                "menu.ders-programi-olusturma.export-pdf",
                "menu.ders-programi-olusturma.import",
                "menu.ders-programi-olusturma.swap",
                "menu.ders-programi-olusturma.lock",
                "menu.ders-programi-olusturma.unlock",

                // ── Kısıtlamalar (exception DAHIL) ──
                "menu.ders-programi-kisitlamalari.create",
                "menu.ders-programi-kisitlamalari.edit",
                "menu.ders-programi-kisitlamalari.delete",
                "menu.ders-programi-kisitlamalari.simulate",
                "menu.ders-programi-kisitlamalari.exception",
                "menu.ders-programi-kisitlamalari.exception-pending",
                "menu.ders-programi-kisitlamalari.exception-approve",
                "menu.ders-programi-kisitlamalari.exception-reject",

                // ── Dönem Programı Aktarım ──
                "menu.donem-programi-aktarim.transfer",

                // ── Program Onayı (Dean'e özel) ──
                "menu.program-onayi.approve",
                "menu.program-onayi.revoke",

                // ── Sınıf Atlatma ──
                "menu.sinif-atlatma-islemleri.promote",
                "menu.sinif-atlatma-islemleri.rollback",
                "menu.sinif-atlatma-islemleri.export",

                // ── Derslik Export ──
                "menu.derslik-doluluk.export",

                // ── Öğrenci ──
                "menu.toplu-danisman-atama.assign",
                "menu.toplu-mufredat-atama.assign",

                // ── Duyuru Yönetimi CRUD ──
                "menu.duyuru-yonetimi.create",
                "menu.duyuru-yonetimi.edit",
                "menu.duyuru-yonetimi.delete",

                // ── Duyuru Kategorileri CRUD ──
                "menu.duyuru-kategorileri.view",
                "menu.duyuru-kategorileri.create",
                "menu.duyuru-kategorileri.edit",
                "menu.duyuru-kategorileri.delete",

                // ── Cross-cutting (authenticated) ──
                // Patch 2A: ders-kayit.view + mufredat-durum.view kaldırıldı.
                // ders-degerlendirme.view Student-only (2026-05-02).
                "menu.ders-programi-kisitlamalari.exception-request",
                "menu.ders-programi-kisitlamalari.exception-my",
                "menu.ders-programi-kisitlamalari.exception-view",

                // ── Cross-cutting (broad role list) ──
                "menu.akilli-planlama.view",
                "menu.akilli-planlama.forecast",
                "menu.akilli-planlama.simulate",
            },

            // SystemAdministrator YOK — Karar 4: locked role, kod düzeyinde
            // RequirePermissionAttribute içinde shortcut ile tüm permission'lara açık.
            //
            // Faz B B3a notu: menu.secmeli-tipleri.* (view/create/edit/delete) sadece SysAdmin'e
            // verilmiştir (Karar #1 — Dean/DepartmentManager CRUD yapamaz). SysAdmin shortcut
            // sayesinde DB satırı gerekmiyor; diğer rollere bilinçli olarak eklenmemiştir.
        };
}
