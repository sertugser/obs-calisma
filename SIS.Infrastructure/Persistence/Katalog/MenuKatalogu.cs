using Microsoft.EntityFrameworkCore;
using SIS.Domain.Identity;

namespace SIS.Infrastructure.Persistence.Katalog;

public sealed record MenuModulKaydi(string Slug, string? UstSlug, string Etiket, string? Simge, int Sira, string? Roller);

public sealed record MenuOgesiKaydi(string ModulSlug, string Etiket, string Yol, string Slug, int Sira, string IzinAnahtari, bool YeniSekmede = false);

// Menü ağacının tek tanımı. Etiket, simge, sıra, roller, yol, etkinlik ve izin bağları yöneticinin
// alanlarıdır (MenuAdminService); katalog yalnız eksik modülü ve öğeyi ekler, hiçbirini güncellemez.
// Yöneticinin sildiği modül ya da öğe geri gelmez.
public static class MenuKatalogu
{
    public static readonly IReadOnlyList<MenuModulKaydi> Moduller =
    [
        new("genel-islemler", null, "Genel İşlemler", "Compass", 1, "Student"),
        new("sik-kullanilanlar", null, "Sık Kullanılanlar", "LayoutGrid", 2, null),
        new("akademik-islemler", null, "Akademik İşlemler", "Calendar", 3, null),
        new("danismanlik-islemleri", null, "Danışmanlık İşlemleri", "Users", 4, null),
        new("ders-ve-donem-islemleri", null, "Ders ve Dönem İşlemleri", "BookOpen", 5, "Student"),
        new("ders-islemleri", null, "Ders İşlemleri", "BookOpen", 6, null),
        new("bolum-islemleri", null, "Bölüm İşlemleri", "Briefcase", 7, null),
        new("ogrenci-islemleri", null, "Öğrenci İşlemleri", "Users", 8, null),
        new("anketler", null, "Anketler", "ClipboardCheck", 9, null),
        new("kaynak-yonetimi", null, "Kaynak Yönetimi", "School", 10, null),
        new("raporlar", null, "Raporlar", "FileBarChart", 105, null),
        new("duyuru-yonetimi-grubu", null, "Duyuru Yönetimi", "Megaphone", 11, null),
        new("hesabim", null, "Hesabım", "User", 12, "Student"),
        new("yonetim", null, "Yönetim", "Shield", 13, null),
        new("yonetim-kullanici-islemleri", "yonetim", "Kullanıcı İşlemleri", "Users", 1, null),
        new("yonetim-genel-ayarlar", "yonetim", "Genel Ayarlar", "Settings", 2, null),
        new("personel-islemleri", null, "Personel İşlemleri", "IdCard", 14, "SystemAdministrator"),
        new("ogretim-islemleri", null, "Öğretim İşlemleri", "GraduationCap", 15, "Instructor,AcademicAdvisor,Dean,DepartmentManager"),
    ];

    public static readonly IReadOnlyList<MenuOgesiKaydi> Ogeler =
    [
        new("genel-islemler", "Akademik Takvim", "/akademik-takvim", "akademik-takvim", 1, "menu.akademik-takvim.view"),
        new("genel-islemler", "Duyurular", "/duyurular", "duyurular", 2, "menu.duyurular.view"),
        new("genel-islemler", "Danışman Bilgileri", "/danisman-bilgileri", "danisman-bilgileri", 3, "menu.danisman-bilgileri.view"),
        new("genel-islemler", "Alınan Dersler", "/alinan-dersler", "alinan-dersler", 4, "menu.alinan-dersler.view"),
        new("genel-islemler", "Açılan Bölüm Dersleri", "/acilan-bolum-dersleri", "acilan-bolum-dersleri", 5, "menu.acilan-bolum-dersleri.view"),
        new("genel-islemler", "Bölüm Ders Programı", "/bolum-ders-programi", "bolum-ders-programi", 6, "menu.bolum-ders-programi.view"),
        new("sik-kullanilanlar", "Ana Sayfa", "/", "ana-sayfa", 1, "menu.ana-sayfa.view"),
        new("akademik-islemler", "Akademik Dönem Tanımları", "/donem-yonetimi", "akademik-donem", 1, "menu.akademik-donem.view"),
        new("akademik-islemler", "Fakülte Tanımları", "/fakulte-tanimlari", "fakulte-tanimlari", 2, "menu.fakulte-tanimlari.view"),
        new("akademik-islemler", "Bölüm Tanımları", "/bolum-tanimlari", "bolum-tanimlari", 3, "menu.bolum-tanimlari.view"),
        new("akademik-islemler", "Program Tanımları", "/program-tanimlari", "program-tanimlari", 4, "menu.program-tanimlari.view"),
        new("akademik-islemler", "Akademik Takvim", "/akademik-takvim", "akademik-takvim-goruntuleme", 5, "menu.akademik-takvim.view"),
        new("akademik-islemler", "Takvim Oluşturucu", "/akademik-takvim-olusturma", "akademik-takvim-olusturma", 6, "menu.akademik-takvim-olusturma.view"),
        new("ders-ve-donem-islemleri", "Ders Kayıt", "/ders-kayit", "ders-kayit", 1, "menu.ders-kayit.view"),
        new("ders-ve-donem-islemleri", "Ders Programım", "/ders-programim", "ders-programim-ogrenci", 2, "menu.ders-programim.view"),
        new("ders-ve-donem-islemleri", "Not Listesi", "/not-listesi", "not-listesi", 4, "menu.not-listesi.view"),
        new("ders-ve-donem-islemleri", "Devamsızlık Durumu", "/devamsizlik-durumu", "devamsizlik-durumu", 5, "menu.devamsizlik-durumu.view"),
        new("ders-ve-donem-islemleri", "Dönem Ortalamaları", "/donem-ortalamalari", "donem-ortalamalari", 6, "menu.donem-ortalamalari.view"),
        new("ders-ve-donem-islemleri", "Müfredat Durum", "/mufredat-durum", "mufredat-durum", 7, "menu.mufredat-durum.view"),
        new("ders-ve-donem-islemleri", "Transkript", "/report/transkript", "transkript", 8, "menu.transkript.view", true),
        new("ders-ve-donem-islemleri", "Transkript Senaryosu", "/transkript-senaryosu", "transkript-senaryosu", 9, "menu.transkript-senaryosu.view"),
        new("ders-islemleri", "Ders Kataloğu", "/ders-katalogu", "ders-katalogu", 1, "menu.ders-katalogu.view"),
        new("ders-islemleri", "Müfredat İşlemleri", "/mufredat-islemleri", "mufredat-islemleri", 2, "menu.mufredat-islemleri.view"),
        new("ders-islemleri", "Müfredat Sınıf Dönem Tanımları", "/mufredat-sinif-donem-tanimlari", "mufredat-sinif-donem", 3, "menu.mufredat-sinif-donem.view"),
        new("ders-islemleri", "Seçmeli Ders Yapılandırması", "/secmeli-yapilandirma", "secmeli-yapilandirma", 4, "menu.secmeli-yapilandirma.view"),
        new("ders-islemleri", "Ders Aç (Müfredat)", "/ders-acma-islemleri", "ders-acma-islemleri", 5, "menu.ders-acma-islemleri.view"),
        new("ders-islemleri", "Ders Aç (Hareket)", "/donem-programi-aktarim", "donem-programi-aktarim", 6, "menu.donem-programi-aktarim.view"),
        new("ders-islemleri", "Şubelendirme İşlemleri", "/subelendirme-islemleri", "subelendirme-islemleri", 7, "menu.subelendirme-islemleri.view"),
        new("ders-islemleri", "Ders Kontenjan İşlemleri", "/ders-kontenjan-islemleri", "ders-kontenjan-islemleri", 8, "menu.ders-kontenjan-islemleri.view"),
        new("ders-islemleri", "Ders Saatleri Yönetimi", "/ders-saatleri-yonetimi", "ders-saatleri-yonetimi", 9, "menu.ders-saatleri-yonetimi.view"),
        new("ders-islemleri", "Program Kısıtlamaları", "/ders-programi-kisitlamalari", "ders-programi-kisitlamalari", 10, "menu.ders-programi-kisitlamalari.view"),
        new("ders-islemleri", "Ders Programı Oluşturma", "/ders-programi-olusturma", "ders-programi-olusturma", 11, "menu.ders-programi-olusturma.view"),
        new("ders-islemleri", "Öğretim Elemanı Atama", "/ogretim-elemani-atama", "ogretim-elemani-atama", 12, "menu.ogretim-elemani-atama.view"),
        new("ders-islemleri", "Müfredat Ders İntibak İşlemleri", "/mufredat-ders-intibak", "mufredat-ders-intibak", 13, "menu.mufredat-ders-intibak.view"),
        new("ders-islemleri", "Ders Veren Öğretim Elemanları", "/ders-veren-ogretim-uyeleri", "ders-veren-ogretim-uyeleri", 14, "menu.ders-veren-ogretim-uyeleri.view"),
        new("ders-islemleri", "Sınav Müfredatı", "/sinav-mufredati", "sinav-mufredati", 15, "menu.sinav-mufredati.view"),
        new("bolum-islemleri", "Şubelendirme İşlemleri", "/subelendirme-islemleri", "subelendirme-islemleri-bolum", 1, "menu.subelendirme-islemleri.view"),
        new("bolum-islemleri", "Ders Veren Öğretim Elemanları", "/ders-veren-ogretim-uyeleri", "ders-veren-ogretim-uyeleri-bolum", 2, "menu.ders-veren-ogretim-uyeleri.view"),
        new("bolum-islemleri", "Toplu Danışman Atama", "/toplu-danisman-atama", "toplu-danisman-atama-bolum", 3, "menu.toplu-danisman-atama.view"),
        new("ogrenci-islemleri", "Öğrenci Rehberi", "/ogrenci-rehberi", "ogrenci-rehberi", 1, "menu.ogrenci-rehberi.view"),
        new("ogrenci-islemleri", "Öğrenci No Değiştirme", "/ogrenci-no-degistirme", "ogrenci-no-degistirme", 2, "menu.ogrenci-no-degistirme.view"),
        new("ogrenci-islemleri", "Sınıf Atlatma İşlemleri", "/sinif-atlatma-islemleri", "sinif-atlatma-islemleri", 3, "menu.sinif-atlatma-islemleri.view"),
        new("ogrenci-islemleri", "Toplu Danışman Atama", "/toplu-danisman-atama", "toplu-danisman-atama", 4, "menu.toplu-danisman-atama.view"),
        new("ogrenci-islemleri", "Toplu Müfredat Atama", "/toplu-mufredat-atama", "toplu-mufredat-atama", 6, "menu.toplu-mufredat-atama.view"),
        new("ogrenci-islemleri", "Ders Kayıt Onay", "/ders-kayit-onay", "ders-kayit-onay", 8, "menu.ders-kayit-onay.view"),
        new("ogrenci-islemleri", "Harf Notu Düzenleme", "/harf-notu-duzenleme", "harf-notu-duzenleme", 11, "menu.harf-notu-duzenleme.view"),
        new("anketler", "Ders Değerlendirme", "/ders-degerlendirme", "ders-degerlendirme", 1, "menu.ders-degerlendirme.view"),
        new("anketler", "Değerlendirme Sonuçları", "/degerlendirme-sonuclari", "degerlendirme-sonuclari", 2, "menu.degerlendirme-sonuclari.view"),
        new("anketler", "Değerlendirme Dönemi Yönetimi", "/degerlendirme-donemi-yonetimi", "degerlendirme-donemi-yonetimi", 3, "menu.degerlendirme-donemi-yonetimi.view"),
        new("kaynak-yonetimi", "Derslik Yönetimi", "/derslik-yonetimi", "derslik-yonetimi", 1, "menu.derslik-yonetimi.view"),
        new("kaynak-yonetimi", "Derslik-Fakülte Ataması", "/derslik-fakulte-atamasi", "derslik-fakulte-atamasi", 2, "menu.derslik-fakulte-atamasi.view"),
        new("kaynak-yonetimi", "Derslik Doluluk Tablosu", "/derslik-doluluk-tablosu", "derslik-doluluk", 3, "menu.derslik-doluluk.view"),
        new("raporlar", "Raporlarım", "/raporlarim", "raporlarim", 1, "menu.raporlarim.view"),
        new("duyuru-yonetimi-grubu", "Duyurular", "/duyurular", "duyurular-admin", 1, "menu.duyurular-admin.view"),
        new("duyuru-yonetimi-grubu", "Duyuru Yönetimi", "/duyuru-yonetimi", "duyuru-yonetimi", 2, "menu.duyuru-yonetimi.view"),
        new("duyuru-yonetimi-grubu", "Duyuru Kategorileri", "/duyuru-kategorileri", "duyuru-kategorileri", 3, "menu.duyuru-kategorileri.view"),
        new("hesabim", "Şifre Değiştir", "/sifre-degistir", "sifre-degistir", 1, "menu.sifre-degistir.view"),
        new("yonetim-kullanici-islemleri", "Kullanıcı Tanımlama", "/kullanici-yonetimi", "kullanici-tanimlama", 1, "menu.kullanici-tanimlama.view"),
        new("yonetim-kullanici-islemleri", "Kullanıcı Yetki Grupları", "/kullanici-yetki-gruplari", "kullanici-yetki-gruplari", 2, "system.permission-bundle.view"),
        new("yonetim-kullanici-islemleri", "Kullanıcı Rapor Grupları", "/kullanici-rapor-gruplari", "kullanici-rapor-gruplari", 3, "menu.kullanici-rapor-gruplari.view"),
        new("yonetim-kullanici-islemleri", "Kullanıcı Vekalet İşlemleri", "/kullanici-vekalet-islemleri", "kullanici-vekalet-islemleri", 6, "menu.kullanici-vekalet-islemleri.view"),
        new("yonetim-kullanici-islemleri", "LOG", "/denetim-kayitlari", "audit-log", 7, "menu.audit-log.view"),
        new("yonetim-genel-ayarlar", "Belge Sınırlandırmalar", "/belge-sinirlandirmalar", "belge-sinirlandirmalar", 3, "menu.belge-sinirlandirmalar.view"),
        new("yonetim-genel-ayarlar", "Web Modül Ayarları", "/web-modul-ayarlari", "web-modul-ayarlari", 4, "menu.web-modul-ayarlari.view"),
        new("yonetim-genel-ayarlar", "Sistem Konfigürasyonu", "/sistem-ayarlari", "system-config", 5, "menu.system-config.view"),
        new("yonetim-genel-ayarlar", "Menü Yönetimi", "/menu-yonetimi", "menu-yonetimi", 8, "menu.menu-yonetimi.view"),
        new("personel-islemleri", "Personel Listesi", "/personel-listesi", "personel-listesi", 1, "menu.personel-listesi.view"),
        new("ogretim-islemleri", "Duyurular", "/duyurular", "duyurular-ogretim", 1, "menu.duyurular.view"),
        new("ogretim-islemleri", "Derslerim", "/derslerim", "derslerim", 2, "menu.derslerim.view"),
        new("ogretim-islemleri", "Ders Programım", "/ders-programim", "ders-programim", 3, "menu.ders-programim.view"),
        new("ogretim-islemleri", "Yoklama Yönetimi", "/yoklama-girisi", "yoklama-girisi", 4, "menu.yoklama-girisi.view"),
        new("ogretim-islemleri", "Sınav Tanımlama", "/sinav-tanimlama", "sinav-tanimlama", 5, "menu.sinav-tanimlama.view"),
        new("ogretim-islemleri", "Not Girişi", "/not-girisi", "not-girisi", 6, "menu.not-girisi.view"),
        new("ogretim-islemleri", "Ders Kısıtları", "/danisman-ders-kisitlari", "danisman-ders-kisitlari", 7, "menu.danisman-ders-kisitlari.view"),
    ];

    public static async Task<IReadOnlyList<KatalogSonucu>> EsitleAsync(AppDbContext db, DateTime simdi, CancellationToken ct = default)
    {
        var sonuclar = new List<KatalogSonucu>();

        // Önce üst modüller, sonra alt modüller: alt modül üstünün kimliğine ihtiyaç duyar.
        foreach (var katman in new[] { Moduller.Where(m => m.UstSlug is null).ToList(), Moduller.Where(m => m.UstSlug is not null).ToList() })
        {
            var modulKimlikleri = await db.MenuModules.ToDictionaryAsync(m => m.Slug, m => m.Id, StringComparer.Ordinal, ct);
            sonuclar.Add(await KatalogEsitleyici.EsitleAsync(db, new KatalogTuru<MenuModule, MenuModulKaydi>
            {
                Ad = "menu-modulu",
                // Üstü olmayan, henüz hiç yazılmamış alt modül atlanır; anahtarı kaydedilmez.
                Kayitlar = katman.Where(m => m.UstSlug is null || modulKimlikleri.ContainsKey(m.UstSlug)).ToList(),
                KayitAnahtari = m => m.Slug,
                VarlikAnahtari = m => m.Slug,
                Olustur = (m, an) => new MenuModule
                {
                    Slug = m.Slug,
                    ParentId = m.UstSlug is null ? null : modulKimlikleri[m.UstSlug],
                    Label = m.Etiket,
                    Icon = m.Simge,
                    SortOrder = m.Sira,
                    AllowedRolesCsv = m.Roller,
                    CreatedAt = an,
                    UpdatedAt = an,
                },
            }, simdi, ct));
        }

        var moduller = await db.MenuModules.ToDictionaryAsync(m => m.Slug, m => m.Id, StringComparer.Ordinal, ct);
        var izinler = await db.Permissions.ToDictionaryAsync(p => p.Key, p => p.Id, StringComparer.Ordinal, ct);
        sonuclar.Add(await KatalogEsitleyici.EsitleAsync(db, new KatalogTuru<MenuItem, MenuOgesiKaydi>
        {
            Ad = "menu-ogesi",
            // Modülü olmayan, henüz hiç yazılmamış öğe atlanır; anahtarı kaydedilmez.
            Kayitlar = Ogeler.Where(o => moduller.ContainsKey(o.ModulSlug)).ToList(),
            KayitAnahtari = o => o.Slug,
            VarlikAnahtari = i => i.Slug,
            Olustur = (o, an) => new MenuItem
            {
                ModuleId = moduller[o.ModulSlug],
                Label = o.Etiket,
                Route = o.Yol,
                Slug = o.Slug,
                SortOrder = o.Sira,
                OpenInNewTab = o.YeniSekmede,
                CreatedAt = an,
                UpdatedAt = an,
                // İzinsiz öğe kimseye görünmez ve sonradan onarılmaz; bu yüzden eksik izin kurulumu durdurur.
                MenuItemPermissions = izinler.TryGetValue(o.IzinAnahtari, out var izinId)
                    ? [new MenuItemPermission { PermissionId = izinId }]
                    : throw new InvalidOperationException(
                        $"Menü öğesi '{o.Slug}' için '{o.IzinAnahtari}' izni veritabanında yok."),
            },
        }, simdi, ct));

        return sonuclar;
    }
}
