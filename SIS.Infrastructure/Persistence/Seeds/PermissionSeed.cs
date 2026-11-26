using Microsoft.EntityFrameworkCore;
using SIS.Domain.Enums;
using SIS.Domain.Identity;

namespace SIS.Infrastructure.Persistence.Seeds;

/// <summary>
/// RBAC Phase 1 — DB seed edilen tüm <see cref="Permission"/> kayıtları.
///
/// Kapsam:
///  - Sadece <c>menu.*</c> permission'ları seed edilir (sidebar item'lara bağlı).
///  - <c>lookup.*</c> permission'ları SEED EDİLMEZ — Karar 7 gereği kod düzeyinde
///    <see cref="Microsoft.AspNetCore.Authorization.IAuthorizationFilter"/> shortcut ile
///    her authenticated user'a açık (`RequirePermissionAttribute` içinde hardcoded).
///  - <c>system.*</c> permission'ları sadece admin UI (PermissionAdminController) için —
///    sidebar'da gözükmez, RolePermissionSeed'e eklenmez. SystemAdmin kod-shortcut ile erişir.
///
/// Inventory: <c>docs/superpowers/plans/2026-04-25-rbac-permission-inventory.md</c> (Sprint 0.1).
/// </summary>
public static class PermissionSeed
{
    /// <summary>
    /// Tüm DB-seed edilen permission tanımları. Sprint 0.1 inventory'sinden derive edildi.
    /// </summary>
    public static readonly IReadOnlyList<(string Key, PermissionCategory Category, string Description)> All =
        new List<(string, PermissionCategory, string)>
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 1: Sık Kullanılanlar
        // ═══════════════════════════════════════════════════════════════════════════
        ("menu.ana-sayfa.view",                          PermissionCategory.Menu, "Ana sayfa görüntüleme"),
        ("menu.akademik-takvim.view",                    PermissionCategory.Menu, "Akademik takvim (öğrenci görünümü) görüntüleme"),
        ("menu.duyurular.view",                          PermissionCategory.Menu, "Duyurular sayfası — Genel İşlemler altındaki link (öğrenci/instructor/advisor)"),
        ("menu.duyurular-admin.view",                    PermissionCategory.Menu, "Duyurular sayfası — Duyuru Yönetimi altındaki link (admin)"),
        ("menu.alinan-dersler.view",                     PermissionCategory.Menu, "Alınan dersler listesi görüntüleme"),
        ("menu.alinan-dersler.statistics",               PermissionCategory.Menu, "Alınan ders not istatistikleri"),
        ("menu.danisman-bilgileri.view",                 PermissionCategory.Menu, "Danışman bilgileri sayfası (öğrenci görünümü)"),
        ("menu.acilan-bolum-dersleri.view",              PermissionCategory.Menu, "Açılan bölüm dersleri sayfası (aktif dönem section listesi)"),
        ("menu.bolum-ders-programi.view",                PermissionCategory.Menu, "Bölüm ders programı (program × sınıf haftalık görünüm) görüntüleme"),
        ("menu.bolum-ders-programi.export",              PermissionCategory.Menu, "Bölüm ders programı PDF indirme"),

        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 2: Akademik İşlemler
        // ═══════════════════════════════════════════════════════════════════════════
        // Akademik Dönem Tanımları
        ("menu.akademik-donem.view",                     PermissionCategory.Menu, "Akademik dönem listesi görüntüleme"),
        ("menu.akademik-donem.create",                   PermissionCategory.Menu, "Yeni akademik dönem oluşturma"),
        ("menu.akademik-donem.edit",                     PermissionCategory.Menu, "Akademik dönem düzenleme"),
        ("menu.akademik-donem.cancel",                   PermissionCategory.Menu, "Akademik dönem iptal etme"),
        ("menu.akademik-donem.uncancel",                 PermissionCategory.Menu, "Akademik dönem iptalini geri alma"),

        // Fakülte Tanımları
        ("menu.fakulte-tanimlari.view",                  PermissionCategory.Menu, "Fakülte listesi görüntüleme"),
        ("menu.fakulte-tanimlari.create",                PermissionCategory.Menu, "Yeni fakülte oluşturma"),
        ("menu.fakulte-tanimlari.edit",                  PermissionCategory.Menu, "Fakülte düzenleme"),
        ("menu.fakulte-tanimlari.delete",                PermissionCategory.Menu, "Fakülte silme"),

        // Bölüm Tanımları
        ("menu.bolum-tanimlari.view",                    PermissionCategory.Menu, "Bölüm listesi görüntüleme"),
        ("menu.bolum-tanimlari.create",                  PermissionCategory.Menu, "Yeni bölüm oluşturma"),
        ("menu.bolum-tanimlari.edit",                    PermissionCategory.Menu, "Bölüm düzenleme"),
        ("menu.bolum-tanimlari.delete",                  PermissionCategory.Menu, "Bölüm silme"),

        // Program Tanımları
        ("menu.program-tanimlari.view",                  PermissionCategory.Menu, "Program listesi görüntüleme"),
        ("menu.program-tanimlari.create",                PermissionCategory.Menu, "Yeni program oluşturma"),
        ("menu.program-tanimlari.edit",                  PermissionCategory.Menu, "Program düzenleme"),
        ("menu.program-tanimlari.delete",                PermissionCategory.Menu, "Program silme"),

        // Takvim Oluşturucu
        ("menu.akademik-takvim-olusturma.view",          PermissionCategory.Menu, "Takvim oluşturucu görüntüleme"),
        ("menu.akademik-takvim-olusturma.edit",          PermissionCategory.Menu, "Şablon/holiday/draft düzenleme"),
        ("menu.akademik-takvim-olusturma.delete",        PermissionCategory.Menu, "Draft silme"),
        ("menu.akademik-takvim-olusturma.generate",      PermissionCategory.Menu, "Yeni takvim üretme"),
        ("menu.akademik-takvim-olusturma.simulate",      PermissionCategory.Menu, "What-if simülasyonu"),
        ("menu.akademik-takvim-olusturma.apply",         PermissionCategory.Menu, "Üretilen takvimi uygula"),
        ("menu.akademik-takvim-olusturma.rollback",      PermissionCategory.Menu, "Uygulanan takvimi geri al"),

        // Sınıf Atlatma Kuralları
        ("menu.sinif-atlatma-kurallari.view",            PermissionCategory.Menu, "Sınıf atlatma kuralları listesi"),
        ("menu.sinif-atlatma-kurallari.create",          PermissionCategory.Menu, "Yeni promotion rule"),
        ("menu.sinif-atlatma-kurallari.edit",            PermissionCategory.Menu, "Promotion rule düzenleme"),
        ("menu.sinif-atlatma-kurallari.delete",          PermissionCategory.Menu, "Promotion rule silme"),

        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 4: Ders ve Dönem İşlemleri (öğrenci tarafı)
        // ═══════════════════════════════════════════════════════════════════════════
        // Ders Kayıt
        ("menu.ders-kayit.view",                         PermissionCategory.Menu, "Ders kayıt sayfası görüntüleme"),
        ("menu.ders-kayit.plan",                         PermissionCategory.Menu, "Ders kayıt planı oluşturma (soft hold)"),
        ("menu.ders-kayit.submit",                       PermissionCategory.Menu, "Ders kayıt onayı gönderme"),
        ("menu.ders-kayit.drop",                         PermissionCategory.Menu, "Ders bırakma (drop)"),
        ("menu.ders-kayit.withdraw",                     PermissionCategory.Menu, "Ders çekilme (withdraw)"),

        // Akıllı Planlama (bölüm planlama özeti + talep tahmini/simülasyonu)
        ("menu.akilli-planlama.view",                    PermissionCategory.Menu, "Bölüm planlama özeti erişimi"),
        ("menu.akilli-planlama.forecast",                PermissionCategory.Menu, "Ders talep tahmini"),
        ("menu.akilli-planlama.simulate",                PermissionCategory.Menu, "Talep simülasyonu"),

        // Diğer öğrenci sayfaları
        ("menu.plan.view",                               PermissionCategory.Menu, "Kayıt planım (soft hold) görüntüleme"),
        ("menu.not-listesi.view",                        PermissionCategory.Menu, "Not listesi (geçmiş) görüntüleme"),
        ("menu.devamsizlik-durumu.view",                 PermissionCategory.Menu, "Devamsızlık durumu görüntüleme"),
        ("menu.donem-ortalamalari.view",                 PermissionCategory.Menu, "Dönem ortalamaları görüntüleme"),
        ("menu.mufredat-durum.view",                     PermissionCategory.Menu, "Müfredat durum + havuz tamamlama"),
        ("menu.transkript.view",                         PermissionCategory.Menu, "Transkript görüntüleme/yazdırma"),
        ("menu.transkript-senaryosu.view",               PermissionCategory.Menu, "Transkript senaryosu (what-if)"),

        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 5: Ders İşlemleri (admin + instructor + advisor)
        // ═══════════════════════════════════════════════════════════════════════════
        // Ders Kataloğu
        ("menu.ders-katalogu.view",                      PermissionCategory.Menu, "Ders kataloğu görüntüleme"),
        ("menu.ders-katalogu.create",                    PermissionCategory.Menu, "Yeni ders ekleme"),
        ("menu.ders-katalogu.edit",                      PermissionCategory.Menu, "Ders düzenleme + prereq-rules"),
        ("menu.ders-katalogu.delete",                    PermissionCategory.Menu, "Ders silme"),
        ("menu.ders-katalogu.prerequisite",              PermissionCategory.Menu, "Ön koşul ekle/sil"),
        ("menu.ders-katalogu.equivalency",               PermissionCategory.Menu, "Eşdeğer ders ekle/sil"),
        ("menu.ders-katalogu.cross-list",                PermissionCategory.Menu, "Cross-list ekle/sil"),
        ("menu.ders-katalogu.access-policy.view",        PermissionCategory.Menu, "Ders erişim politikası listesi"),
        ("menu.ders-katalogu.access-policy.create",      PermissionCategory.Menu, "Ders erişim politikası oluştur"),
        ("menu.ders-katalogu.access-policy.delete",      PermissionCategory.Menu, "Ders erişim politikası sil"),
        ("menu.ders-katalogu.access-rule.view",          PermissionCategory.Menu, "Ders erişim kuralı listesi"),
        ("menu.ders-katalogu.access-rule.create",        PermissionCategory.Menu, "Ders erişim kuralı oluştur"),
        ("menu.ders-katalogu.access-rule.edit",          PermissionCategory.Menu, "Ders erişim kuralı düzenle"),
        ("menu.ders-katalogu.access-rule.delete",        PermissionCategory.Menu, "Ders erişim kuralı sil"),

        // Müfredat İşlemleri
        ("menu.mufredat-islemleri.view",                 PermissionCategory.Menu, "Müfredat listesi/detay görüntüleme"),
        ("menu.mufredat-islemleri.create",               PermissionCategory.Menu, "Yeni müfredat oluşturma"),
        ("menu.mufredat-islemleri.edit",                 PermissionCategory.Menu, "Müfredat düzenleme"),
        ("menu.mufredat-islemleri.delete",               PermissionCategory.Menu, "Müfredat silme"),
        ("menu.mufredat-islemleri.add-course",           PermissionCategory.Menu, "Müfredata ders ekleme"),
        ("menu.mufredat-islemleri.edit-course",          PermissionCategory.Menu, "Müfredat dersi düzenleme"),
        ("menu.mufredat-islemleri.remove-course",        PermissionCategory.Menu, "Müfredattan ders çıkarma"),
        ("menu.mufredat-islemleri.copy",                 PermissionCategory.Menu, "Müfredat kopyalama"),
        ("menu.mufredat-islemleri.activate",             PermissionCategory.Menu, "Müfredat aktifleştirme"),
        ("menu.mufredat-islemleri.create-pool",          PermissionCategory.Menu, "Seçmeli havuz ekleme"),
        ("menu.mufredat-islemleri.edit-pool",            PermissionCategory.Menu, "Seçmeli havuz düzenleme"),
        ("menu.mufredat-islemleri.delete-pool",          PermissionCategory.Menu, "Seçmeli havuz silme"),
        ("menu.mufredat-islemleri.publish",              PermissionCategory.Menu, "Müfredat yayınlama"),
        ("menu.mufredat-islemleri.archive",              PermissionCategory.Menu, "Müfredat arşivleme"),

        // Müfredat Ders İntibak İşlemleri (standalone toplu intibak — PreviousCourse eşleştirmesi)
        ("menu.mufredat-ders-intibak.view",              PermissionCategory.Menu, "Müfredat ders intibak (önceki ders eşleştirmesi) görüntüleme"),
        ("menu.mufredat-ders-intibak.edit",              PermissionCategory.Menu, "Müfredat ders intibak toplu kaydetme"),

        // Müfredat Sınıf Dönem Tanımları
        ("menu.mufredat-sinif-donem.view",               PermissionCategory.Menu, "Müfredat term-rules listesi"),
        ("menu.mufredat-sinif-donem.create",             PermissionCategory.Menu, "Yeni term-rule oluşturma"),
        ("menu.mufredat-sinif-donem.edit",               PermissionCategory.Menu, "Term-rule düzenleme"),
        ("menu.mufredat-sinif-donem.delete",             PermissionCategory.Menu, "Term-rule silme"),

        // Seçmeli Ders Grupları
        ("menu.secmeli-ders-gruplari.view",              PermissionCategory.Menu, "Elective grup listesi"),
        ("menu.secmeli-ders-gruplari.create",            PermissionCategory.Menu, "Yeni elective grup"),
        ("menu.secmeli-ders-gruplari.edit",              PermissionCategory.Menu, "Elective grup düzenleme"),
        ("menu.secmeli-ders-gruplari.delete",            PermissionCategory.Menu, "Elective grup silme"),
        ("menu.secmeli-ders-gruplari.add-course",        PermissionCategory.Menu, "Elective gruba ders ekleme"),
        ("menu.secmeli-ders-gruplari.remove-course",     PermissionCategory.Menu, "Elective gruptan ders çıkarma"),

        // Seçmeli Ders Yapılandırması (meta-permission — birleşik sayfa erişimi)
        ("menu.secmeli-yapilandirma.view",               PermissionCategory.Menu, "Seçmeli ders yapılandırma sayfası (Gruplar + Tipler tab wrapper)"),

        // Seçmeli Tipleri (Faz B B3a — DB-driven lookup, sadece SysAdmin CRUD)
        ("menu.secmeli-tipleri.view",                    PermissionCategory.Menu, "Seçmeli tip listesi"),
        ("menu.secmeli-tipleri.create",                  PermissionCategory.Menu, "Yeni seçmeli tip"),
        ("menu.secmeli-tipleri.edit",                    PermissionCategory.Menu, "Seçmeli tip düzenleme"),
        ("menu.secmeli-tipleri.delete",                  PermissionCategory.Menu, "Seçmeli tip silme (soft delete)"),

        // Ders Aç (Müfredat)
        ("menu.ders-acma-islemleri.view",                PermissionCategory.Menu, "Ders açma listesi + curriculum/instructors lookup"),
        ("menu.ders-acma-islemleri.open",                PermissionCategory.Menu, "Tek ders açma (section)"),
        ("menu.ders-acma-islemleri.bulk-open",           PermissionCategory.Menu, "Toplu ders açma"),
        ("menu.ders-acma-islemleri.edit",                PermissionCategory.Menu, "Section düzenleme"),
        ("menu.ders-acma-islemleri.close-section",       PermissionCategory.Menu, "Section kapatma"),
        ("menu.ders-acma-islemleri.close-all",           PermissionCategory.Menu, "Tüm dersi kapatma"),
        ("menu.ders-acma-islemleri.reactivate",          PermissionCategory.Menu, "Section yeniden aktifleştirme"),

        // Sınav Müfredatı (program seviyesi sınav yönergeleri — ExamRegulationController)
        ("menu.sinav-mufredati.view",                    PermissionCategory.Menu, "Sınav yönergesi görüntüleme"),
        ("menu.sinav-mufredati.edit",                    PermissionCategory.Menu, "Sınav yönergesi düzenleme"),
        ("menu.sinav-mufredati.create",                  PermissionCategory.Menu, "Sınav yönergesi oluşturma"),
        ("menu.sinav-mufredati.delete",                  PermissionCategory.Menu, "Sınav yönergesi silme"),

        // Sınav Tanımlama (şube bazlı sınav oturumu — SectionExamController)
        ("menu.sinav-tanimlama.view",                    PermissionCategory.Menu, "Sınav Tanımlama sayfası (sınav oturumlarını görme)"),
        ("menu.sinav-tanimlama.manage",                  PermissionCategory.Menu, "Sınav oturumu ekle/düzenle/sil (tarih/derslik/ilan)"),

        // Şubelendirme İşlemleri (sidebar item; backend henüz yok ama placeholder)
        ("menu.subelendirme-islemleri.view",             PermissionCategory.Menu, "Şubelendirme işlemleri sayfası"),
        ("menu.subelendirme-islemleri.transfer",         PermissionCategory.Menu, "Şubeler arası öğrenci taşıma"),
        ("menu.subelendirme-islemleri.auto-split",       PermissionCategory.Menu, "Otomatik şubelendirme (auto-split)"),

        // Ders Kontenjan İşlemleri (sidebar placeholder)
        ("menu.ders-kontenjan-islemleri.view",           PermissionCategory.Menu, "Ders kontenjan işlemleri sayfası"),

        // Ders Saatleri Yönetimi
        ("menu.ders-saatleri-yonetimi.view",             PermissionCategory.Menu, "Time slot listesi"),
        ("menu.ders-saatleri-yonetimi.create",           PermissionCategory.Menu, "Yeni time slot"),
        ("menu.ders-saatleri-yonetimi.edit",             PermissionCategory.Menu, "Time slot düzenleme"),
        ("menu.ders-saatleri-yonetimi.delete",           PermissionCategory.Menu, "Time slot silme"),
        ("menu.ders-saatleri-yonetimi.regenerate",       PermissionCategory.Menu, "Time slot grid yeniden üret"),

        // Ders Programı Kısıtlamaları
        ("menu.ders-programi-kisitlamalari.view",        PermissionCategory.Menu, "Constraint görüntüleme (distribution dahil)"),
        ("menu.ders-programi-kisitlamalari.create",      PermissionCategory.Menu, "Yeni constraint/distribution"),
        ("menu.ders-programi-kisitlamalari.edit",        PermissionCategory.Menu, "Constraint parametre/category/weight düzenleme"),
        ("menu.ders-programi-kisitlamalari.delete",      PermissionCategory.Menu, "Constraint/distribution silme"),
        ("menu.ders-programi-kisitlamalari.toggle",      PermissionCategory.Menu, "Constraint aktif/pasif toggle"),
        ("menu.ders-programi-kisitlamalari.simulate",    PermissionCategory.Menu, "Constraint simülasyon (move/suggest/change)"),
        ("menu.ders-programi-kisitlamalari.exception",   PermissionCategory.Menu, "Constraint exception oluştur/sil"),
        ("menu.ders-programi-kisitlamalari.exception-request",  PermissionCategory.Menu, "Exception talebi oluştur (authenticated)"),
        ("menu.ders-programi-kisitlamalari.exception-my",       PermissionCategory.Menu, "Kendi exception talepleri"),
        ("menu.ders-programi-kisitlamalari.exception-view",     PermissionCategory.Menu, "Exception talep detayı"),
        ("menu.ders-programi-kisitlamalari.exception-pending",  PermissionCategory.Menu, "Onay bekleyen exception talepleri"),
        ("menu.ders-programi-kisitlamalari.exception-approve",  PermissionCategory.Menu, "Exception talebini onayla"),
        ("menu.ders-programi-kisitlamalari.exception-reject",   PermissionCategory.Menu, "Exception talebini reddet"),

        // Ders Programı Oluşturma
        ("menu.ders-programi-olusturma.view",            PermissionCategory.Menu, "Faculty planning + scheduling görüntüleme"),
        ("menu.ders-programi-olusturma.assign",          PermissionCategory.Menu, "Section ataması"),
        ("menu.ders-programi-olusturma.unassign",        PermissionCategory.Menu, "Section atama iptali"),
        ("menu.ders-programi-olusturma.reset-all",       PermissionCategory.Menu, "Faculty/term reset-all"),
        ("menu.ders-programi-olusturma.auto-schedule",   PermissionCategory.Menu, "Otomatik schedule (Faculty Planning V2)"),
        ("menu.ders-programi-olusturma.apply",           PermissionCategory.Menu, "Schedule uygulama"),
        ("menu.ders-programi-olusturma.validate",        PermissionCategory.Menu, "Schedule validate (existing + new)"),
        ("menu.ders-programi-olusturma.suggest",         PermissionCategory.Menu, "Schedule önerisi"),
        ("menu.ders-programi-olusturma.export",          PermissionCategory.Menu, "Excel export"),
        ("menu.ders-programi-olusturma.export-pdf",      PermissionCategory.Menu, "PDF export"),
        ("menu.ders-programi-olusturma.import",          PermissionCategory.Menu, "Schedule import (preview + apply)"),
        ("menu.ders-programi-olusturma.swap",            PermissionCategory.Menu, "Section swap"),
        ("menu.ders-programi-olusturma.lock",            PermissionCategory.Menu, "Section lock"),
        ("menu.ders-programi-olusturma.unlock",          PermissionCategory.Menu, "Section unlock"),

        // Öğretim Elemanı Atama
        ("menu.ogretim-elemani-atama.view",              PermissionCategory.Menu, "Öğretim elemanı atama + workload"),
        ("menu.ogretim-elemani-atama.bulk-assign",       PermissionCategory.Menu, "Toplu öğretim elemanı atama"),

        // Dönem Programı Aktarım
        ("menu.donem-programi-aktarim.view",             PermissionCategory.Menu, "Önceki dönem dersleri görüntüleme"),
        ("menu.donem-programi-aktarim.transfer",         PermissionCategory.Menu, "Önceki dönemden ders aktarma"),

        // Derslik Doluluk Tablosu
        ("menu.derslik-doluluk.view",                    PermissionCategory.Menu, "Derslik doluluk tablosu (scheduling dashboard)"),
        ("menu.derslik-doluluk.export",                  PermissionCategory.Menu, "Derslik doluluk CSV export"),

        // Ders Veren Öğretim Üyeleri (sidebar placeholder)
        ("menu.ders-veren-ogretim-uyeleri.view",         PermissionCategory.Menu, "Ders veren öğretim üyeleri sayfası"),

        // Program Onayı
        ("menu.program-onayi.view",                      PermissionCategory.Menu, "Program onayı durum görüntüleme"),
        ("menu.program-onayi.approve",                   PermissionCategory.Menu, "Faculty schedule onayla"),
        ("menu.program-onayi.revoke",                    PermissionCategory.Menu, "Faculty schedule onayını geri al"),

        // Sınıf Listeleri sayfası 2026-05-11'de silindi; aynı veri Şubelendirme İşlemleri (subelendirme-islemleri) sayfasında.

        // Dönem Kapatma — sayfa silindi; .view AcademicProgressionController tarafından kullanılmaya devam ediyor
        ("menu.donem-kapatma.view",                      PermissionCategory.Menu, "Sınıf atlatma simülasyonu (AcademicProgressionController)"),

        // Derslerim (instructor + paylaşımlı)
        ("menu.derslerim.view",                          PermissionCategory.Menu, "Derslerim sayfası (my-sections, profile, context)"),

        // Yoklama Yönetimi
        ("menu.yoklama-girisi.view",                      PermissionCategory.Menu, "Yoklama yönetimi sayfası, geçmiş yoklamalar ve QR yoklama yönetimi"),

        // Ders Programım
        ("menu.ders-programim.view",                     PermissionCategory.Menu, "Ders programım görüntüleme"),
        ("menu.ders-programim.edit",                     PermissionCategory.Menu, "Section schedule apply"),

        // Not Girişi
        ("menu.not-girisi.view",                         PermissionCategory.Menu, "Not girişi sayfası (components/grades/criteria)"),
        ("menu.not-girisi.edit",                         PermissionCategory.Menu, "Bileşen + grading criteria düzenleme"),
        ("menu.not-girisi.submit",                       PermissionCategory.Menu, "Toplu not gönderme"),
        ("menu.not-girisi.export",                       PermissionCategory.Menu, "Excel şablon indirme"),
        ("menu.not-girisi.import",                       PermissionCategory.Menu, "Excel import (preview + apply)"),
        ("menu.not-girisi.finalize",                     PermissionCategory.Menu, "Section sonuçlandırma"),
        ("menu.not-girisi.unfinalize",                   PermissionCategory.Menu, "Sonuçlandırmayı geri alma"),
        ("menu.not-girisi.publish",                      PermissionCategory.Menu, "Komponent ilanı (öğrenciye yayın aç/kapa)"),

        // Danışman Ders Kısıtları
        ("menu.danisman-ders-kisitlari.view",            PermissionCategory.Menu, "Advisor ders kısıtları görüntüleme"),
        ("menu.danisman-ders-kisitlari.create",          PermissionCategory.Menu, "Yeni advisor kısıt"),
        ("menu.danisman-ders-kisitlari.edit",            PermissionCategory.Menu, "Advisor kısıt düzenleme"),
        ("menu.danisman-ders-kisitlari.delete",          PermissionCategory.Menu, "Advisor kısıt silme"),

        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 6: Öğrenci İşlemleri
        // ═══════════════════════════════════════════════════════════════════════════
        ("menu.ogrenci-rehberi.view",                    PermissionCategory.Menu, "Öğrenci listesi/detay görüntüleme"),
        ("menu.ogrenci-rehberi.create",                  PermissionCategory.Menu, "Yeni öğrenci oluşturma"),
        ("menu.ogrenci-rehberi.edit-academic",           PermissionCategory.Menu, "Öğrenci akademik bilgisi düzenleme"),
        ("menu.ogrenci-rehberi.edit-personal",           PermissionCategory.Menu, "Öğrenci kişisel bilgisi düzenleme"),
        ("menu.ogrenci-rehberi.edit-documents",          PermissionCategory.Menu, "Öğrenci belgesi yönetimi"),
        ("menu.ogrenci-rehberi.generate-documents",      PermissionCategory.Menu, "Öğrenci belgesi üretme"),
        ("menu.ogrenci-rehberi.edit-scholarship",        PermissionCategory.Menu, "Öğrenci burs bilgisi düzenleme"),
        ("menu.ogrenci-rehberi.edit-placement",          PermissionCategory.Menu, "Öğrenci yerleştirme bilgisi düzenleme"),
        ("menu.ogrenci-rehberi.integration",             PermissionCategory.Menu, "Öğrenci NVİ/YÖKSİS entegrasyon sorguları"),

        // Sidebar placeholder sayfaları (Faz 4 — backend stub)
        ("menu.ogrenci-no-degistirme.view",              PermissionCategory.Menu, "Öğrenci no değiştirme sayfası"),
        ("menu.ogrenci-no-degistirme.change",            PermissionCategory.Menu, "Öğrenci numarası değiştirme"),
        ("menu.sinif-atlatma-islemleri.view",            PermissionCategory.Menu, "Sınıf atlatma işlemleri sayfası"),
        ("menu.sinif-atlatma-islemleri.promote",         PermissionCategory.Menu, "Sınıf atlatma uygulama"),
        ("menu.sinif-atlatma-islemleri.rollback",        PermissionCategory.Menu, "Sınıf atlatmayı geri alma"),
        ("menu.sinif-atlatma-islemleri.export",          PermissionCategory.Menu, "Promotion sonuçlarını export"),
        ("menu.toplu-danisman-atama.view",               PermissionCategory.Menu, "Toplu danışman atama sayfası"),
        ("menu.toplu-danisman-atama.assign",             PermissionCategory.Menu, "Tek/toplu danışman ataması"),
        ("menu.toplu-mufredat-atama.view",               PermissionCategory.Menu, "Toplu müfredat atama sayfası"),
        ("menu.toplu-mufredat-atama.assign",             PermissionCategory.Menu, "Toplu müfredat ataması"),
        ("menu.ders-kayit-onay.view",               PermissionCategory.Menu, "Danışman ders kayıt onay listesi + öğrenciler"),
        ("menu.ders-kayit-onay.approve",            PermissionCategory.Menu, "Ders kayıt onaylama"),
        ("menu.ders-kayit-onay.reject",             PermissionCategory.Menu, "Ders kayıt reddetme"),
        ("menu.harf-notu-duzenleme.view",                PermissionCategory.Menu, "Harf notu düzenleme sayfası"),

        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 7: Anketler
        // ═══════════════════════════════════════════════════════════════════════════
        ("menu.ders-degerlendirme.view",                 PermissionCategory.Menu, "Ders değerlendirme listesi/active period"),
        ("menu.ders-degerlendirme.submit",               PermissionCategory.Menu, "Ders değerlendirme gönderme"),
        ("menu.degerlendirme-sonuclari.view",            PermissionCategory.Menu, "Değerlendirme sonuçları (section/department)"),
        ("menu.degerlendirme-donemi-yonetimi.view",      PermissionCategory.Menu, "Değerlendirme dönemi yönetimi listesi"),
        ("menu.degerlendirme-donemi-yonetimi.open",      PermissionCategory.Menu, "Değerlendirme dönemi açma"),
        ("menu.degerlendirme-donemi-yonetimi.close",     PermissionCategory.Menu, "Değerlendirme dönemi kapatma"),
        ("menu.degerlendirme-donemi-yonetimi.create",    PermissionCategory.Menu, "Değerlendirme sorusu oluşturma"),
        ("menu.degerlendirme-donemi-yonetimi.edit",      PermissionCategory.Menu, "Değerlendirme sorusu düzenleme"),
        ("menu.degerlendirme-donemi-yonetimi.delete",    PermissionCategory.Menu, "Değerlendirme sorusu silme"),

        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 8: Kaynak Yönetimi
        // ═══════════════════════════════════════════════════════════════════════════
        ("menu.derslik-yonetimi.view",                   PermissionCategory.Menu, "Derslik listesi/detay"),
        ("menu.derslik-yonetimi.create",                 PermissionCategory.Menu, "Yeni derslik"),
        ("menu.derslik-yonetimi.edit",                   PermissionCategory.Menu, "Derslik düzenleme"),
        ("menu.derslik-yonetimi.delete",                 PermissionCategory.Menu, "Derslik silme"),
        ("menu.derslik-fakulte-atamasi.view",            PermissionCategory.Menu, "Derslik-fakülte ataması listesi"),
        ("menu.derslik-fakulte-atamasi.suggest",         PermissionCategory.Menu, "Atama önerisi"),
        ("menu.derslik-fakulte-atamasi.create",          PermissionCategory.Menu, "Atama oluştur (tek/toplu)"),
        ("menu.derslik-fakulte-atamasi.delete",          PermissionCategory.Menu, "Atama sil"),

        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 9: Duyuru Yönetimi
        // ═══════════════════════════════════════════════════════════════════════════
        ("menu.duyuru-yonetimi.view",                    PermissionCategory.Menu, "Internal announcement (all) listesi"),
        ("menu.duyuru-yonetimi.create",                  PermissionCategory.Menu, "Yeni internal announcement"),
        ("menu.duyuru-yonetimi.edit",                    PermissionCategory.Menu, "Internal announcement düzenleme"),
        ("menu.duyuru-yonetimi.delete",                  PermissionCategory.Menu, "Internal announcement silme"),

        // Duyuru Kategorileri (Madde 3)
        ("menu.duyuru-kategorileri.view",                PermissionCategory.Menu, "Duyuru kategorileri listesi"),
        ("menu.duyuru-kategorileri.create",              PermissionCategory.Menu, "Yeni duyuru kategorisi"),
        ("menu.duyuru-kategorileri.edit",                PermissionCategory.Menu, "Duyuru kategorisi düzenleme"),
        ("menu.duyuru-kategorileri.delete",              PermissionCategory.Menu, "Duyuru kategorisi silme"),

        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 10: Hesabım
        // ═══════════════════════════════════════════════════════════════════════════
        ("menu.sifre-degistir.view",                     PermissionCategory.Menu, "Şifre değiştir sayfası (herkes)"),

        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 11.1: Yönetim → Kullanıcı İşlemleri
        // ═══════════════════════════════════════════════════════════════════════════
        ("menu.kullanici-tanimlama.view",                PermissionCategory.Menu, "Kullanıcı listesi/detay"),
        ("menu.kullanici-tanimlama.update-roles",        PermissionCategory.Menu, "Kullanıcı rolleri güncelleme"),
        ("menu.kullanici-tanimlama.reset-password",      PermissionCategory.Menu, "Kullanıcı şifre sıfırlama"),
        ("menu.kullanici-tanimlama.set-status",          PermissionCategory.Menu, "Kullanıcı aktif/pasif durumu"),
        ("menu.personel-listesi.view",                   PermissionCategory.Menu, "Personel (instructor) listesi"),
        ("menu.personel-listesi.create",                 PermissionCategory.Menu, "Yeni personel oluşturma"),
        // "Kullanıcı Yetki Grupları" sayfası system.permission-bundle.view ile korunur (M-1 standardizasyon).
        ("menu.kullanici-rapor-gruplari.view",           PermissionCategory.Menu, "Kullanıcı rapor grupları sayfası"),
        ("menu.kullanici-vekalet-islemleri.view",        PermissionCategory.Menu, "Kullanıcı vekalet işlemleri sayfası"),
        ("menu.kullanici-vekalet-islemleri.manage",      PermissionCategory.Menu, "Vekâlet oluşturma/iptal etme (audit'li)"),
        ("menu.audit-log.view",                          PermissionCategory.Menu, "Denetim kayıtları listesi/stats/anomalies"),
        ("menu.audit-log.export",                        PermissionCategory.Menu, "Denetim kayıtları CSV export"),

        // ═══════════════════════════════════════════════════════════════════════════
        // Modül 11.2: Yönetim → Genel Ayarlar
        // ═══════════════════════════════════════════════════════════════════════════
        ("menu.belge-sinirlandirmalar.view",             PermissionCategory.Menu, "Belge sınırlandırmalar sayfası"),
        ("menu.web-modul-ayarlari.view",                 PermissionCategory.Menu, "Web modül ayarları sayfası"),
        ("menu.web-modul-ayarlari.edit",                 PermissionCategory.Menu, "Web modül ayarları düzenleme"),
        ("menu.web-modul-ayarlari.export",               PermissionCategory.Menu, "Web modül ayarları export"),
        ("menu.web-modul-ayarlari.import",               PermissionCategory.Menu, "Web modül ayarları import"),
        ("menu.system-config.view",                      PermissionCategory.Menu, "Sistem konfigürasyonu görüntüleme"),
        ("menu.system-config.edit",                      PermissionCategory.Menu, "Sistem konfigürasyonu düzenleme"),
        ("menu.system-config.export",                    PermissionCategory.Menu, "Sistem konfigürasyonu export"),
        ("menu.system-config.import",                    PermissionCategory.Menu, "Sistem konfigürasyonu import"),

        // ─── system.* (Phase 1) ─────────────────────────────────────────────
        // Sidebar'da görünmeyen, sadece SystemAdministrator'ın admin UI'da kullandığı permission'lar.
        // Sprint 2 PermissionAdminController bu key'leri kullanır. RolePermissionSeed'e eklenmez —
        // SystemAdmin kod-shortcut ile zaten otomatik erişir (Karar 4).
        ("system.permission.view",                       PermissionCategory.System, "Permission tablosu görüntüleme (admin UI)"),
        ("system.permission.assign",                     PermissionCategory.System, "Role permission atama (admin UI)"),

        // ─── system.* (Phase 2 — Permission Bundles) ────────────────────────
        ("system.permission-bundle.view",                PermissionCategory.System, "Yetki paketi listesi/detayı görüntüleme (admin UI)"),
        ("system.permission-bundle.manage",              PermissionCategory.System, "Yetki paketi oluşturma/düzenleme/silme (admin UI)"),
        ("system.permission-bundle.apply",               PermissionCategory.System, "Yetki paketini role uygulama/kaldırma (admin UI)"),
        ("system.user-scope.view",                       PermissionCategory.System, "Kullanıcı scope bilgisini görüntüleme (admin UI)"),
        ("system.user-scope.assign",                     PermissionCategory.System, "Kullanıcı scope atama/güncelleme/temizleme (admin UI)"),

        // Not: "Kullanıcı Yetki Grupları" sayfası system.permission-bundle.* anahtarlarını kullanır
        // (yukarıda Phase 2 bloğunda). Eski menu.kullanici-yetki-gruplari.* anahtarları kaldırıldı —
        // .view yukarıda Kullanıcı İşlemleri bloğunda zaten tanımlı, .manage hiç kullanılmıyordu.

        // ─── menu.* + system.* (Menü Yönetimi — Admin Menu Editor) ─────────
        ("menu.menu-yonetimi.view",                      PermissionCategory.Menu, "Menü yönetimi sayfası görüntüleme"),
        ("system.menu.view",                             PermissionCategory.System, "Menü ağacı (modül/item) okuma (admin UI)"),
        ("system.menu.manage",                           PermissionCategory.System, "Menü modül/item oluşturma/düzenleme/silme/sıralama (admin UI)"),

        // ─── menu.* (Phase 3 — Rapor Yetkilendirmesi, Kullanıcı Rapor Grupları sayfası altında) ─
        ("menu.kullanici-rapor-gruplari.manage",         PermissionCategory.Menu, "Rapor kategorisi/raporu CRUD + kategori-rol matrix + per-rapor override yönetimi"),

        // ─── menu.* (Phase 3 — Raporlarım son kullanıcı sayfası) ─
        ("menu.raporlarim.view",                         PermissionCategory.Menu, "Kullanıcının erişebildiği raporları listeleyen 'Raporlarım' sayfası"),
    };
}
