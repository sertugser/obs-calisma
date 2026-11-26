using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedConstraintDefinitionsAsync(AppDbContext context)
    {
        var definitions = new List<ConstraintDefinition>
        {
            // ── Hard Constraints (15) ───────────────────────────────
            new()
            {
                Code = "H3",
                Name = "Kapasite Kontrolü",
                Description = "Derslik kapasitesinin şube kontenjanını karşılayıp karşılamadığını kontrol eder.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 1
            },
            new()
            {
                Code = "H4",
                Name = "Derslik Tipi Uyumu",
                Description = "Dersin gerektirdiği derslik tipinin (lab, amfi, normal) atanan derslikle uyumunu kontrol eder.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                // 2026-05-19: AmphiThresholdEnabled + ClassroomTypeRequirementEnabled alt-toggle'lar
                // SystemConfigs'ten H4.Parameters JSON'a taşındı (tek otorite = Kısıtlar sayfası).
                Parameters = """{"AmphiThreshold": 100, "AmphiThresholdEnabled": false, "ClassroomTypeRequirementEnabled": false}""",
                SortOrder = 2
            },
            new()
            {
                Code = "H10",
                Name = "Fakülte Derslik Ataması",
                Description = "Dersliklerin dönemlik fakülte atamasına uygun kullanılıp kullanılmadığını kontrol eder.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 3
            },
            new()
            {
                Code = "H1",
                Name = "Derslik Çakışması",
                Description = "Aynı dersliğe aynı zaman diliminde birden fazla ders atanmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 4
            },
            new()
            {
                Code = "H2",
                Name = "Öğretim Üyesi Çakışması",
                Description = "Aynı öğretim üyesine aynı zaman diliminde birden fazla ders atanmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 5
            },
            new()
            {
                Code = "H5",
                Name = "Müfredat Çakışması",
                Description = "Aynı müfredattaki zorunlu derslerin aynı zaman dilimine atanmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                // 2026-05-19: CurriculumConflictMode SystemConfigs'ten H5.Parameters JSON'a taşındı.
                Parameters = """{"CurriculumConflictMode": "cohort", "EnrollmentConflictHardThreshold": 1}""",
                SortOrder = 6
            },
            new()
            {
                Code = "H6",
                Name = "Ders Bileşen Çakışması",
                Description = "Aynı dersin teorik ve uygulama bileşenlerinin çakışmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                // 2026-05-19: MaxLabBlockHours SystemConfigs'ten H6.Parameters JSON'a taşındı.
                Parameters = """{"MaxLabBlockHours": 2}""",
                SortOrder = 7
            },
            new()
            {
                Code = "H8",
                Name = "Aynı Ders Şube Çakışması",
                Description = "Aynı dersin farklı şubelerinin aynı zaman dilimine atanmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 8
            },
            new()
            {
                Code = "H-WL",
                Name = "Haftalık Yük Limiti",
                Description = "Öğretim üyesinin haftalık ders yükünün YÖK unvanına göre belirlenen limiti aşmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 9
            },
            new()
            {
                Code = "H-DL",
                Name = "Günlük Yük Limiti",
                Description = "Öğretim üyesinin günlük ders yükünün belirlenen limiti aşmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"MaxDailyHours": 8}""",
                SortOrder = 10
            },
            new()
            {
                Code = "H-DO",
                Name = "Boş Gün Zorunluluğu",
                Description = "Öğretim üyesinin haftada en az bir boş gün almasını zorunlu kılar.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 11
            },
            new()
            {
                Code = "H11",
                Name = "Eşkoşul Uyumu",
                Description = "Eşkoşul (corequisite) derslerin aynı dönemde ve çakışmadan programlanmasını kontrol eder.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 12
            },
            new()
            {
                Code = "H12",
                Name = "Kredi-Saat Uyumu",
                Description = "Dersin kredi/AKTS değerine uygun sayıda slot atanıp atanmadığını kontrol eder.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 13
            },
            new()
            {
                Code = "H13",
                Name = "Öğle Arası Koruması",
                Description = "Öğle arası saatlerine ders atanmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"LunchBreakStart": "11:40", "LunchBreakEnd": "12:20"}""",
                SortOrder = 14
            },
            new()
            {
                Code = "H15",
                Name = "Ardışık Ders Limiti",
                Description = "Öğretim üyesinin aralıksız ardışık ders sayısının belirlenen limiti aşmasını engeller.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"MaxConsecutiveHours": 4}""",
                SortOrder = 15
            },
            new()
            {
                Code = "H16",
                Name = "Teori-Lab Gün Ayrımı",
                Description = "Dersin teori ve lab bileşenlerinin toplam saati eşiği aşıyorsa farklı günlere atanmasını zorunlu kılar.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"TheoryLabSplitThreshold": 4}""",
                SortOrder = 16
            },
            new()
            {
                Code = "H17",
                Name = "Online-Only Slot Koruması",
                Description = "AllowedMode=OnlineOnly damgalı slotlara (akşam saatleri, hafta sonu vb.) yüz-yüze ders atanmasını engeller. Slot modları Ders Saatleri Yönetimi sayfasından admin tarafından yönetilir; hybrid ve online dersler bu kuraldan muaftır.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                SortOrder = 17
            },

            // ── Soft Constraints (9) ────────────────────────────────
            new()
            {
                Code = "S1",
                Name = "Doluluk Oranı Optimizasyonu",
                Description = "Derslik doluluk oranının hedef aralıkta (%60-%85) olmasını teşvik eder.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"S1MinOccupancy": 60, "S1MaxOccupancy": 85}""",
                SortOrder = 101
            },
            // Plan 10 Phase 2 — S2 ConstraintDefinition kaldırıldı.
            // H-DL "Günlük Yük Limiti" Category toggle (Hard/Soft) ile iki davranışı yönetiyor:
            //   - Hard (Zorunlu): DailyLoadEncoder yasaklar (limit aşımı kabul edilmez).
            //   - Soft (Tercihli): DailyLoadSoftEncoder S2Weight ile penalty üretir.
            new()
            {
                Code = "S3",
                Name = "Slot Yoğunluk Kontrolü",
                Description = "Popüler zaman dilimlerinde aşırı yığılmayı önlemeye çalışır.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"S3MaxSectionsPerSlot": 5}""",
                SortOrder = 103
            },
            new()
            {
                Code = "S4",
                Name = "Birlikte Alınan Ders Kontrolü",
                Description = "Farklı bölümlerden sık birlikte alınan derslerin çakışmamasını teşvik eder.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"S4MinCoPairThreshold": 20}""",
                SortOrder = 104
            },
            // Plan 10 — S5 ConstraintDefinition kaldırıldı.
            // H13 "Öğle Arası Koruması" Category toggle (Hard/Soft) ile iki davranışı yönetiyor:
            //   - Hard (Zorunlu): LunchBreakEncoder yasaklar.
            //   - Soft (Tercihli): LunchBreakSoftEncoder S5Weight ile penalty üretir.
            new()
            {
                Code = "S7",
                Name = "Öğrenci Boşluk Minimizasyonu",
                Description = "Öğrencilerin gün içindeki ders arası boşluklarını minimize etmeye çalışır.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"S7MaxGapSlots": 3}""",
                SortOrder = 106
            },
            new()
            {
                Code = "S8",
                Name = "Öğretim Üyesi Gün Dağılımı",
                Description = "Öğretim üyesinin derslerinin haftaya dengeli yayılmasını teşvik eder.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"Weight": 1}""",
                SortOrder = 107
            },
            new()
            {
                Code = "S12",
                Name = "Hafta İçi Denge",
                Description = "Derslerin hafta içi günlere dengeli dağılmasını teşvik eder.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"Weight": 1}""",
                SortOrder = 109
            },
            new()
            {
                Code = "S20",
                Name = "Komşu Yıl Çakışma Garantisi",
                Description = "Komşu sınıfların (1-2, 2-3, 3-4) zorunlu dersleri arasında öğrencinin alabileceği en az bir alternatif şube kombinasyonu kalmalı. Bir öğrenci alttan kalan veya üstten almak istediği zorunlu bir dersin en az bir şubesi, kendi sınıfının zorunlu dersleriyle çakışmamalı. Seçmeli dersler bu kuralın dışında.",
                Category = "Hard",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = null,
                SortOrder = 110
            },
            new()
            {
                Code = "S21",
                Name = "Online Ders Slot Tercihi",
                Description = "Online derslerin OnlineOnly damgalı slotlara (akşam saatleri, hafta sonu vb.) yerleştirilmesini TEŞVİK eder. Online ders Both (her ikisine açık) slot'a yerleşirse her blok için ceza alır; OnlineOnly slot'a yerleşirse ceza yok. Yetersiz OnlineOnly slot durumunda otomatik olarak Both slot'a kayar (esnek, hard değil).",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"Weight": 5}""",
                SortOrder = 111
            },
            new()
            {
                Code = "DIST",
                Name = "İlişkisel Kısıtlamalar",
                Description = "Dersler arası ilişkisel kuralları (aynı gün, ardışık, aynı derslik, sıralama) kontrol eder.",
                Category = "Soft",
                Scope = "Scheduling",
                IsEnabled = true,
                Parameters = """{"Weight": 3}""",
                SortOrder = 112
            },
        };

        // Katalog kuralı: açma-kapama, ağırlık, parametreler, kategori ve sıra yöneticinin (kısıt ekranı);
        // katalog yalnız ad ve açıklamayı günceller.
        await Katalog.KatalogEsitleyici.EsitleAsync(context, new Katalog.KatalogTuru<ConstraintDefinition, ConstraintDefinition>
        {
            Ad = "kisit",
            Kayitlar = definitions,
            KayitAnahtari = d => d.Code,
            VarlikAnahtari = d => d.Code,
            Olustur = (d, _) => d,
            KatalogAlanlariniYaz = (v, d) =>
            {
                if (v.Name == d.Name && v.Description == d.Description) return false;
                v.Name = d.Name;
                v.Description = d.Description;
                return true;
            },
        }, DateTime.UtcNow);
    }
}
