using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIS.Application.Common.Time;
using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;
using SIS.Infrastructure.Persistence;
using SIS.Infrastructure.Services;

namespace SIS.Tests.Postgres.DersKaydi;

/// <summary>
/// Ders kaydı testleri için gerçek PostgreSQL veri kurucu: kayıt penceresi açık bir etkin dönem (2026-2027 Bahar, tarihler 2027),
/// bir bölümün dersleri, şubeler ve programsız etkin öğrenciler. Dönem, migration verisindeki etkin dönemden sonra
/// başlar (etkin dönem olarak en geç başlayan seçilir). Saat sabittir; her adım kendi context'iyle yazar,
/// testler belirli bir Id'ye dayanmaz.
/// </summary>
internal sealed class DersKaydiVeriKurucu(PostgresFixture fixture)
{
    private static readonly DateTime Simdi = new(2027, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private int _bolumId, _donemId;

    public static async Task<DersKaydiVeriKurucu> KurAsync(PostgresFixture fixture)
    {
        var veri = new DersKaydiVeriKurucu(fixture);
        veri._bolumId = (await veri.KaydetAsync(new Department
        {
            Name = "Deneme Bölümü", Code = "DB", Faculty = new Faculty { Name = "Deneme Fakültesi", Code = "DF" }
        })).Id;
        veri._donemId = (await veri.KaydetAsync(new AcademicTerm
        {
            Name = "2026-2027 Bahar", ShortEnglishName = "Spring 2026", TermType = TermType.Spring, Year = 2026, IsActivated = true,
            TermStart = Utc(2027, 2, 15), TermEnd = Utc(2027, 6, 15),
            AcademicCalendar = new TermAcademicCalendar
            {
                RegistrationStart = Utc(2027, 3, 1), RegistrationEnd = Utc(2027, 3, 20),
                AddDropStart = Utc(2027, 3, 21), AddDropEnd = Utc(2027, 3, 28), WithdrawalDeadline = Utc(2027, 5, 10),
                GradeEntryStart = Utc(2027, 6, 1), GradeEntryEnd = Utc(2027, 6, 29), CreatedAt = Utc(2027, 1, 1)
            }
        })).Id;
        return veri;
    }

    public async Task<int> DersEkleAsync(string kod) =>
        (await KaydetAsync(new Course { Code = kod, Name = kod + " Dersi", Ects = 5, NationalCredit = 3, DepartmentId = _bolumId })).Id;

    /// <summary>Etkin dönemde boş, açık bir şube; <paramref name="grup"/> verilirse çapraz liste grubunda.</summary>
    public async Task<int> SubeEkleAsync(int dersId, int kapasite, int? grup = null) => (await KaydetAsync(new CourseSection
    {
        CourseId = dersId, TermId = _donemId, SectionNumber = 1, Capacity = kapasite,
        Status = CourseSectionStatus.Active, CrossListGroupId = grup, CreatedAt = Simdi
    })).Id;

    public async Task<int> OgrenciEkleAsync(string no)
    {
        var kullanici = await KaydetAsync(new ApplicationUser { UserName = "ogrenci" + no, FirstName = "Deneme", LastName = "Öğrenci" });
        return (await KaydetAsync(new Student
        {
            UserId = kullanici.Id, StudentNumber = "2023" + no, NationalId = "1000000000" + no, Status = StudentStatus.Active,
            CompletedSemesters = 2, Cgpa = 3.00m, Sgpa = 3.00m, CreatedAt = Simdi
        })).Id;
    }

    /// <summary>Verilen context'le gerçek doğrulama ve kayıt servisleri üzerinden tek şubelik gönderim.</summary>
    public Task<RegistrationResponseDto> GonderAsync(AppDbContext db, int ogrenciId, int subeId) =>
        new CourseRegistrationService(db, new RegistrationValidationService(db, new SabitSaat()), new SabitSaat())
            .SubmitRegistrationAsync(ogrenciId, new RegistrationSubmitDto { CourseSectionIds = [subeId] });

    /// <summary>Verilen kesiciyi taşıyan ayrı bir context.</summary>
    public AppDbContext KesiciliContext(ArayaGirenKesici kesici) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.BaglantiCumlesi).AddInterceptors(kesici).Options);

    /// <summary>Şube mevcudu, yeni bir context'le.</summary>
    public async Task<int> MevcutAsync(int subeId)
    {
        await using var db = fixture.YeniContext();
        return await db.CourseSections.Where(s => s.Id == subeId).Select(s => s.EnrolledCount).SingleAsync();
    }

    /// <summary>Şubedeki kayıtlar (öğrenci, durum), yeni bir context'le.</summary>
    public async Task<(int, RegistrationStatus)[]> KayitlarAsync(int subeId)
    {
        await using var db = fixture.YeniContext();
        var kayitlar = await db.CourseRegistrations.Where(r => r.CourseSectionId == subeId)
            .OrderBy(r => r.StudentId).Select(r => new { r.StudentId, r.Status }).ToListAsync();
        return kayitlar.Select(r => (r.StudentId, r.Status)).ToArray();
    }

    /// <summary>Yeni bir context'le gerçek doğrulama servisi üzerinden tek şubelik doğrulama (gönderim yok).</summary>
    public async Task<ValidationResultDto> DogrulaAsync(int ogrenciId, int subeId)
    {
        await using var db = fixture.YeniContext();
        return await new RegistrationValidationService(db, new SabitSaat()).ValidateRegistrationAsync(ogrenciId, [subeId]);
    }

    /// <summary>Şubenin mevcudunu kayıt satırı oluşturmadan verir.</summary>
    public async Task MevcutVerAsync(int subeId, int mevcut)
    {
        await using var db = fixture.YeniContext();
        await db.CourseSections.Where(s => s.Id == subeId).ExecuteUpdateAsync(u => u.SetProperty(s => s.EnrolledCount, mevcut));
    }

    /// <summary>Etkin olmayan önceki bir dönemde, verilen grupta, kapasitesi dolu bir şube.</summary>
    public async Task<int> BaskaDonemDoluSubeEkleAsync(int dersId, int kapasite, int grup)
    {
        var donem = await KaydetAsync(new AcademicTerm
        {
            Name = "2026-2027 Güz", ShortEnglishName = "Fall 2026", TermType = TermType.Fall, Year = 2026,
            TermStart = Utc(2026, 9, 15), TermEnd = Utc(2027, 1, 15)
        });
        return (await KaydetAsync(new CourseSection
        {
            CourseId = dersId, TermId = donem.Id, SectionNumber = 1, Capacity = kapasite, EnrolledCount = kapasite,
            Status = CourseSectionStatus.Active, CrossListGroupId = grup, CreatedAt = Simdi
        })).Id;
    }

    /// <summary>
    /// Şube tablosuna ayrı bir derste 1000 boş şube ekler ve tablonun istatistiğini günceller: şubeye göre okuma,
    /// istatistik olsa da olmasa da birincil anahtar üzerinden yapılır. Ölçülen şubeler bundan SONRA eklenmelidir.
    /// </summary>
    public async Task DolguSubeleriEkleAsync()
    {
        var dersId = await DersEkleAsync("DLG900");
        await using var db = fixture.YeniContext();
        db.CourseSections.AddRange(Enumerable.Range(1, 1000).Select(no => new CourseSection
        {
            CourseId = dersId, TermId = _donemId, SectionNumber = no, Capacity = 1, Status = CourseSectionStatus.Active, CreatedAt = Simdi
        }));
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("ANALYZE \"CourseSections\"");
    }

    private static DateTime Utc(int yil, int ay, int gun) => new(yil, ay, gun, 0, 0, 0, DateTimeKind.Utc);

    private async Task<T> KaydetAsync<T>(T varlik) where T : class
    {
        await using var db = fixture.YeniContext();
        db.Add(varlik);
        await db.SaveChangesAsync();
        return varlik;
    }

    private sealed class SabitSaat : IDateTimeProvider
    {
        public DateTime UtcNow => Simdi;
    }
}

/// <summary>Bağlı context'in işlem içindeki ilk SaveChanges'inden hemen önce verilen eylemi bir kez çalıştırır.</summary>
internal sealed class ArayaGirenKesici(Func<Task> eylem) : SaveChangesInterceptor
{
    public int Calisma { get; private set; }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData olay, InterceptionResult<int> sonuc, CancellationToken ct = default)
    {
        if (Calisma == 0 && olay.Context!.Database.CurrentTransaction is not null)
        {
            Calisma++;
            await eylem();
        }
        return sonuc;
    }
}
