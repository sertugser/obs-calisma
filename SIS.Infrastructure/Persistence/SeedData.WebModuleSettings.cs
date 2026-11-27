using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    public static async Task SeedWebModuleSettingsAsync(AppDbContext context)
    {
        var settings = new List<WebModuleSetting>
        {
            new()
            {
                ModuleKey = "StudentPanel",
                Key = "CourseRegistrationOpenAt",
                Value = "09:00",
                Category = "Registration",
                DataType = "time",
                Description = "Öğrenci panelinde ders kayıt ekranının günlük açılış saati"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "StudentTopCount",
                Value = "5",
                Category = "Announcements",
                DataType = "int",
                Description = "Öğrenci panelinde üstte gösterilecek duyuru sayısı"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "PinnedAnnouncementsFirst",
                Value = "true",
                Category = "Announcements",
                DataType = "bool",
                Description = "Sabitlenmiş duyurular listelerde önce gösterilsin mi?"
            },
            new()
            {
                ModuleKey = "StudentCourseRegistration",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Öğrenci ders kayıt modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "StudentCourseRegistration",
                Key = "Announcement",
                Value = "Ders kayıt işlemleri aktif dönem ve akademik takvim koşullarına göre yürütülür.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Öğrenci ders kayıt modülü kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "StudentAddDrop",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Öğrenci ders ekle/bırak modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "StudentAddDrop",
                Key = "Announcement",
                Value = "Ders ekle/bırak işlemleri akademik takvimdeki ilgili pencereye göre açılır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Öğrenci ders ekle/bırak modülü kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "StudentWithdrawal",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Öğrenci dersten çekilme modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "StudentWithdrawal",
                Key = "Announcement",
                Value = "Dersten çekilme işlemleri sadece çekilme penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Öğrenci dersten çekilme modülü kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "InstructorGradeEntry",
                Key = "Mode",
                Value = "grade-entry-period",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Not girişi modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "InstructorGradeEntry",
                Key = "Announcement",
                Value = "Not girişi işlemleri yalnızca not giriş süre tanımlarında açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Not girişi modülü kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "InstructorStudentPhone",
                Key = "Mode",
                Value = "closed",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Öğrenci telefon görünürlüğü modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "InstructorStudentPhone",
                Key = "Announcement",
                Value = "Öğrenci telefon bilgileri öğretim elemanlarına gösterilmez.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Öğrenci telefon görünürlüğü kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "AnnouncementsFeed",
                Key = "Mode",
                Value = "open",
                Category = "ModuleControl",
                DataType = "string",
                Description = "İç duyuru akışının çalışma modu"
            },
            new()
            {
                ModuleKey = "AnnouncementsFeed",
                Key = "Announcement",
                Value = "İç duyuru akışı aktiftir.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "İç duyuru akışı kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "NewStudentCourseRegistration",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Yeni öğrenci ders kayıt modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "NewStudentCourseRegistration",
                Key = "Announcement",
                Value = "Yeni öğrenci ders kayıt işlemleri sadece ilgili takvim penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Yeni öğrenci ders kayıt modülü kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "ExtendedStudentCourseRegistration",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Öğrenim süresini aşan öğrenci ders kayıt modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "ExtendedStudentCourseRegistration",
                Key = "Announcement",
                Value = "Öğrenim süresini aşan öğrencilerin kayıt işlemleri yalnızca ilgili takvim penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Öğrenim süresini aşan öğrenci ders kayıt modülü kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "AdvisorRegistrationApproval",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Danışman kayıt onayı modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "AdvisorRegistrationApproval",
                Key = "Announcement",
                Value = "Danışman kayıt onayları yalnızca ilgili takvim penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Danışman kayıt onayı kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "AdvisorNewStudentRegistrationApproval",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Yeni öğrenci danışman kayıt onayı modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "AdvisorNewStudentRegistrationApproval",
                Key = "Announcement",
                Value = "Yeni öğrenci danışman onayları yalnızca ilgili takvim penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Yeni öğrenci danışman kayıt onayı kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "NewStudentAddDrop",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Yeni öğrenci ders ekle/bırak modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "NewStudentAddDrop",
                Key = "Announcement",
                Value = "Yeni öğrenci ders ekle/bırak işlemleri yalnızca ilgili takvim penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Yeni öğrenci ders ekle/bırak modülü kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "AdvisorAddDropApproval",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Danışman ekle/bırak onayı modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "AdvisorAddDropApproval",
                Key = "Announcement",
                Value = "Ders ekle/bırak onayları yalnızca ilgili takvim penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Danışman ekle/bırak onayı kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "MidtermGradeEntry",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Ara sınav not giriş modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "MidtermGradeEntry",
                Key = "Announcement",
                Value = "Ara sınav not girişi yalnızca ilgili takvim penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Ara sınav not girişi kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "MidtermGradePublication",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Ara sınav not yayın modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "MidtermGradePublication",
                Key = "Announcement",
                Value = "Ara sınav notları henüz öğrenciye yayınlanamaz.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Ara sınav not yayın kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "MidtermExcuseGradeEntry",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Ara sınav mazeret not giriş modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "MidtermExcuseGradeEntry",
                Key = "Announcement",
                Value = "Ara sınav mazeret not girişi yalnızca ilgili takvim penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Ara sınav mazeret not girişi kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "MidtermExcuseGradePublication",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Ara sınav mazeret not yayın modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "MidtermExcuseGradePublication",
                Key = "Announcement",
                Value = "Ara sınav mazeret notları henüz öğrenciye yayınlanamaz.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Ara sınav mazeret not yayın kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "FinalGradeEntry",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Yarıyıl sınav not giriş modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "FinalGradeEntry",
                Key = "Announcement",
                Value = "Yarıyıl sınav not girişi yalnızca ilgili takvim penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Yarıyıl sınav not girişi kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "FinalGradePublication",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Yarıyıl sınav not yayın modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "FinalGradePublication",
                Key = "Announcement",
                Value = "Yarıyıl sınav notları henüz öğrenciye yayınlanamaz.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Yarıyıl sınav not yayın kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "MakeupGradeEntry",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Bütünleme not giriş modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "MakeupGradeEntry",
                Key = "Announcement",
                Value = "Bütünleme not girişi yalnızca ilgili takvim penceresinde açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Bütünleme not girişi kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "MakeupGradePublication",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Bütünleme not yayın modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "MakeupGradePublication",
                Key = "Announcement",
                Value = "Bütünleme notları henüz öğrenciye yayınlanamaz.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Bütünleme not yayın kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "LetterGradePublication",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Harf notu yayın modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "LetterGradePublication",
                Key = "Announcement",
                Value = "Harf notları henüz öğrenciye yayınlanamaz.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Harf notu yayın kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "MakeupLetterGradePublication",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Bütünleme harf notu yayın modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "MakeupLetterGradePublication",
                Key = "Announcement",
                Value = "Bütünleme harf notları henüz öğrenciye yayınlanamaz.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Bütünleme harf notu yayın kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "SocialTranscriptApplicationEvaluation",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Sosyal transkript başvuru değerlendirme modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "SocialTranscriptApplicationEvaluation",
                Key = "Announcement",
                Value = "Sosyal transkript değerlendirme akışı yalnızca not yayınlama sonrası açık olacaktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Sosyal transkript değerlendirme kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "SocialTranscriptStudentObjection",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Sosyal transkript öğrenci itiraz modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "SocialTranscriptStudentObjection",
                Key = "Announcement",
                Value = "Sosyal transkript itiraz akışı yalnızca not yayınlama sonrası açık olacaktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Sosyal transkript itiraz kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "RegistrationFreezeApplication",
                Key = "Mode",
                Value = "term-event-based",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Kayıt dondurma başvuru modülünün çalışma modu"
            },
            new()
            {
                ModuleKey = "RegistrationFreezeApplication",
                Key = "Announcement",
                Value = "Kayıt dondurma başvurusu yalnızca aktif dönemde dondurma son gününe kadar açıktır.",
                Category = "ModuleControl",
                DataType = "string",
                Description = "Kayıt dondurma başvuru kullanıcı duyurusu"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "MaxTitleLength",
                Value = "200",
                Category = "Announcements",
                DataType = "int",
                Description = "Duyuru başlığı için izin verilen maksimum karakter sayısı"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "MaxContentLength",
                Value = "50000",
                Category = "Announcements",
                DataType = "int",
                Description = "Duyuru içeriği için izin verilen maksimum karakter sayısı (HTML dahil)"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "AllowImagesInContent",
                Value = "true",
                Category = "Announcements",
                DataType = "bool",
                Description = "Zengin metin içeriğinde resim eklenebilir mi?"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "AllowTablesInContent",
                Value = "true",
                Category = "Announcements",
                DataType = "bool",
                Description = "Zengin metin içeriğinde tablo eklenebilir mi?"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "StudentYearFilterMin",
                Value = "1",
                Category = "Announcements",
                DataType = "int",
                Description = "Sınıf bazlı duyuru hedeflemesinde seçilebilecek en küçük sınıf yılı (örn. 0 = Hazırlık)"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "StudentYearFilterMax",
                Value = "7",
                Category = "Announcements",
                DataType = "int",
                Description = "Sınıf bazlı duyuru hedeflemesinde seçilebilecek en büyük sınıf yılı"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "AttachmentsEnabled",
                Value = "true",
                Category = "Announcements",
                DataType = "bool",
                Description = "Duyurulara dosya eklenebilir mi?"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "AttachmentMaxSizeMB",
                Value = "20",
                Category = "Announcements",
                DataType = "int",
                Description = "Bir dosya ekinin izin verilen maksimum boyutu (MB)"
            },
            new()
            {
                ModuleKey = "Announcements",
                Key = "AttachmentMaxCount",
                Value = "5",
                Category = "Announcements",
                DataType = "int",
                Description = "Bir duyuruya iliştirilebilecek maksimum dosya sayısı"
            },
        };

        // Katalog kuralı: eksik anahtar eklenir, yöneticinin sildiği geri gelmez. Var olan satıra
        // dokunulmaz (değer ve geçerlilik tarihleri yöneticinin); kataloğun alanları Adım 3.4b'de.
        await Katalog.KatalogEsitleyici.EsitleAsync(context, new Katalog.KatalogTuru<WebModuleSetting, WebModuleSetting>
        {
            Ad = "web-modul-ayari",
            Kayitlar = settings,
            KayitAnahtari = s => $"{s.ModuleKey}:{s.Key}",
            VarlikAnahtari = s => $"{s.ModuleKey}:{s.Key}",
            Olustur = (s, _) => s,
        }, DateTime.UtcNow);
    }
}
