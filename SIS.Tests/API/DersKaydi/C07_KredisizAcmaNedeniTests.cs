using SIS.Application.CourseRegistration.DTOs;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Tests.API.DersKaydi;

// C-07: kredisiz açma nedeninin okunduğu yerler (program süzgeci, kimlik eşleşmesi, bayrak, anlık görüntü), bugünkü davranış.
// Sınır 30 (AGNO 1,90, üretim varsayılanı); talep edilen 30 AKTS + 10 AKTS'lik ders, ders kredisiz sayılmazsa ECTSCap'e takılır.
public class C07_KredisizAcmaNedeniTests
{
    private static async Task<(bool Basarili, string[] Kurallar)> Calistir(DersKaydiOrtami o, bool gonderim, params CourseSection[] subeler)
    {
        var ids = subeler.Select(s => s.Id).ToList();
        if (gonderim)
        {
            var y = await o.Kayit().SubmitRegistrationAsync(o.Ogrenci.Id, new RegistrationSubmitDto { CourseSectionIds = ids });
            return (y.Success, y.ValidationResult?.Errors.Select(e => e.Rule).ToArray() ?? []);
        }
        var s = await o.Dogrulama().ValidateRegistrationAsync(o.Ogrenci.Id, ids);
        return (s.IsValid, s.Errors.Select(e => e.Rule).ToArray());
    }

    private static CourseRegistration Kayit(DersKaydiOrtami o, int subeId) => o.Db.CourseRegistrations.Single(r => r.CourseSectionId == subeId);

    // Nedeni yalnız eski program tanımlıyorsa ders kredisiz sayılmaz (30 + 10 > 30); ana ve etkin programın müfredatında da tanımlanınca düşer.
    // Eski kayıt (ana olmayan ya da etkin olmayan) ana ve etkin kayıttan önce oluşturulur: süzgeç kalkarsa sıradaki ilk kayıt o olur.
    [Theory]
    [InlineData(StudentProgramType.DoubleMajor, true)]
    [InlineData(StudentProgramType.Main, false)]
    public async Task C07_KredisizNeden_YalnizAnaVeEtkinProgramdanOkunur(StudentProgramType eskiTur, bool eskiEtkin)
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        o.AnaProgramiDegistir(eskiTur, eskiEtkin);
        var sube = o.Sube(o.DersEkle("XXX101", 30));
        var kredisizDers = o.DersEkle("KKK101", 10);
        var kredisiz = o.Sube(kredisizDers);
        o.KredisizMufredataEkle(kredisizDers);

        var ret = await Calistir(o, false, sube, kredisiz);
        Assert.False(ret.Basarili);
        Assert.Equal(new[] { "ECTSCap" }, ret.Kurallar);

        o.AnaProgramMufredatinaKredisizEkle(kredisizDers);
        var kabul = await Calistir(o, false, sube, kredisiz);
        Assert.True(kabul.Basarili);
        Assert.Empty(kabul.Kurallar);
    }

    // Ders ve şube kimlikleri ayrışık: kredisiz sayma ders kimliğiyle eşleşir (şube kimliğiyle değil); neden kalkınca ret.
    [Fact]
    public async Task C07_KredisizTalepEdilenDers_DersKimligiyleEslesir()
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        o.DersEkle("AAA101");
        o.DersEkle("AAA102");
        var sube = o.Sube(o.DersEkle("XXX101", 30));
        var kredisizDers = o.DersEkle("KKK101", 10);
        var kredisiz = o.Sube(kredisizDers);
        Assert.NotEqual(kredisizDers.Id, kredisiz.Id);
        var mufredatDersi = o.KredisizMufredataEkle(kredisizDers);

        var kabul = await Calistir(o, false, sube, kredisiz);
        Assert.True(kabul.Basarili);
        Assert.Empty(kabul.Kurallar);

        mufredatDersi.OpeningReason = null;
        o.Db.SaveChanges();
        var ret = await Calistir(o, false, sube, kredisiz);
        Assert.False(ret.Basarili);
        Assert.Equal(new[] { "ECTSCap" }, ret.Kurallar);
    }

    // Toplamdan düşmeyi yalnız IsCreditFree belirler: kredisiz=false + çakışma muafiyeti=true ise ders sayılır (ret); ters bayrak durumunda düşer.
    [Fact]
    public async Task C07_KredisizTalepEdilenDers_YalnizKredisizBayragiBelirler()
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        var sube = o.Sube(o.DersEkle("XXX101", 30));
        var kredisizDers = o.DersEkle("KKK101", 10);
        var kredisiz = o.Sube(kredisizDers);
        var neden = o.KredisizMufredataEkle(kredisizDers).OpeningReason!;

        neden.IsCreditFree = false;
        neden.BypassScheduleConflict = true;
        o.Db.SaveChanges();
        var ret = await Calistir(o, true, sube, kredisiz);
        Assert.False(ret.Basarili);
        Assert.Equal(new[] { "ECTSCap" }, ret.Kurallar);
        Assert.Empty(o.Db.CourseRegistrations);

        neden.IsCreditFree = true;
        neden.BypassScheduleConflict = false;
        o.Db.SaveChanges();
        var kabul = await Calistir(o, true, sube, kredisiz);
        Assert.True(kabul.Basarili);
        Assert.Empty(kabul.Kurallar);
        Assert.Equal(2, o.Db.CourseRegistrations.Count());
    }

    // Mevcut kaydın kredisiz anlık görüntüsü veritabanından yüklenir (izlenen kayıt yok): 25 kredisiz + 30 geçer; anlık görüntü kredisiz değilse ret.
    [Fact]
    public async Task C07_KredisizMevcutKayit_AnlikGoruntuVeritabanindanYuklenir()
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        var mevcut = o.KayitEkle(o.DersEkle("MMM101", 25), RegistrationStatus.Active);
        var neden = o.KredisizNeden();
        mevcut.OpeningReasonSnapshot = neden;
        o.Db.SaveChanges();
        var sube = o.Sube(o.DersEkle("XXX101", 30));
        var subeId = sube.Id;

        o.Db.ChangeTracker.Clear();
        var kabul = await Calistir(o, true, sube);
        Assert.True(kabul.Basarili);
        Assert.Empty(kabul.Kurallar);
        Assert.Equal(RegistrationStatus.PendingAdvisorApproval, Kayit(o, subeId).Status);

        o.Db.CourseRegistrations.Remove(Kayit(o, subeId));
        o.Db.CurriculumCourseOpeningReasons.Single(r => r.Id == neden.Id).IsCreditFree = false;
        o.Db.SaveChanges();
        o.Db.ChangeTracker.Clear();
        var ret = await Calistir(o, true, sube);
        Assert.False(ret.Basarili);
        Assert.Equal(new[] { "ECTSCap" }, ret.Kurallar);
        Assert.DoesNotContain(o.Db.CourseRegistrations, r => r.CourseSectionId == subeId);
    }

    // Gönderim, nedeni kayda anlık görüntü olarak yazar (nedensiz ders: boş). Nedeni müfredattan kaldırıp 30 AKTS'lik ikinci dersi göndermek,
    // kredisiz kaydın toplamdan yine düştüğünü gösterir (kanıt anlık görüntüden gelir).
    [Fact]
    public async Task C07_Gonderim_AcmaNedeniniKayitaYazar()
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        var kredisizDers = o.DersEkle("KKK101", 10);
        var kredisiz = o.Sube(kredisizDers);
        var duz = o.Sube(o.DersEkle("DDD101", 1));
        var sube = o.Sube(o.DersEkle("XXX101", 29));
        var mufredatDersi = o.KredisizMufredataEkle(kredisizDers);
        var nedenId = mufredatDersi.OpeningReason!.Id;
        var (kredisizId, duzId) = (kredisiz.Id, duz.Id);

        Assert.True((await Calistir(o, true, kredisiz, duz)).Basarili);
        o.Db.ChangeTracker.Clear();
        Assert.Equal(nedenId, Kayit(o, kredisizId).OpeningReasonIdSnapshot);
        Assert.Null(Kayit(o, duzId).OpeningReasonIdSnapshot);

        o.Db.CurriculumCourses.Single(c => c.CurriculumId == mufredatDersi.CurriculumId && c.CourseId == mufredatDersi.CourseId).OpeningReasonId = null;
        o.Db.SaveChanges();
        var kabul = await Calistir(o, true, sube);
        Assert.True(kabul.Basarili);
        Assert.Empty(kabul.Kurallar);
    }

    // Anlık görüntü yalnız ana ve etkin programın müfredatından alınır: nedeni yalnız eski program tanımlayan ders kayda boş yazılır
    // (gönderim kabul edilir); aynı neden ana ve etkin programda tanımlı dersin kaydına yazılır. Eski kayıt önce oluşturulur (süzgeç kalkarsa ilk o gelir).
    [Theory]
    [InlineData(StudentProgramType.DoubleMajor, true)]
    [InlineData(StudentProgramType.Main, false)]
    public async Task C07_Gonderim_AcmaNedeniniYalnizAnaVeEtkinProgramdanYazar(StudentProgramType eskiTur, bool eskiEtkin)
    {
        using var o = new DersKaydiOrtami(agno: 1.90m);
        o.AnaProgramiDegistir(eskiTur, eskiEtkin);
        var eskideDers = o.DersEkle("EEE101", 5);
        var eskide = o.Sube(eskideDers);
        var anaDers = o.DersEkle("AAA101", 5);
        var ana = o.Sube(anaDers);
        o.KredisizMufredataEkle(eskideDers);
        var anaNedenId = o.AnaProgramMufredatinaKredisizEkle(anaDers).OpeningReason!.Id;
        var (eskideId, anaId) = (eskide.Id, ana.Id);

        var yanit = await Calistir(o, true, eskide, ana);
        Assert.True(yanit.Basarili);
        Assert.Empty(yanit.Kurallar);
        o.Db.ChangeTracker.Clear();
        Assert.Null(Kayit(o, eskideId).OpeningReasonIdSnapshot);
        Assert.Equal(anaNedenId, Kayit(o, anaId).OpeningReasonIdSnapshot);
    }
}
