using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SIS.Tests.Mimari;

/// <summary>
/// Katman yapısını sabitleyen mimari testler (CLAUDE.md §2-3).
/// Bugünkü ihlaller <see cref="BilinenIhlaller"/> içindedir; liste yalnız küçülür.
/// Her kural iki yönlü çalışır: listede olmayan ihlal testi kırar (yeni ihlal),
/// listede olup artık ihlal olmayan kayıt da testi kırar (ölü istisna).
/// Ölçüm yalnız yansımadır: K1/K2 derlemenin başvuru tablosuna, K3 tür imzalarına bakar.
/// </summary>
public class MimariKuralTestleri
{
    private static readonly Assembly DomainDerlemesi = typeof(global::SIS.Domain.Entities.AcademicTerm).Assembly;
    private static readonly Assembly ApplicationDerlemesi = typeof(global::SIS.Application.Grading.ExamCohort).Assembly;
    private static readonly Assembly ApiDerlemesi = typeof(global::SIS.API.Controllers.GradeController).Assembly;

    /// <summary>
    /// K2 izin listesi (CLAUDE.md:11): SIS.Application'ın System.* dışında başvurabileceği derlemeler.
    /// Bu listeye eklemek mimari karardır. Sıralı (ordinal) tutulur.
    /// </summary>
    internal static readonly string[] ApplicationIzinListesi =
    [
        "FluentValidation",
        "SIS.Domain",
    ];

    private const BindingFlags TumUyeler = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void K1_Domain_yalniz_System_derlemelerine_basvurur()
    {
        var bulunan = IzinsizBasvurular(DomainDerlemesi, []);

        BilinenlerleKarsilastir(
            bulunan,
            BilinenIhlaller.K1DomainBasvurulari,
            ad => $"K1 yeni ihlal (CLAUDE.md:10, :32 Domain çatıdan bağımsız): SIS.Domain izin verilmeyen derlemeye başvuruyor: {ad}",
            ad => $"K1 ölü istisna: SIS.Domain artık {ad} derlemesine başvurmuyor; BilinenIhlaller.K1DomainBasvurulari'ndan düşün (liste yalnız küçülür)");
    }

    [Fact]
    public void K2_Application_yalniz_Domain_ve_izin_listesindeki_derlemelere_basvurur()
    {
        var bulunan = IzinsizBasvurular(ApplicationDerlemesi, ApplicationIzinListesi);

        BilinenlerleKarsilastir(
            bulunan,
            BilinenIhlaller.K2ApplicationBasvurulari,
            ad => $"K2 yeni ihlal (CLAUDE.md:11 Application → yalnız Domain): SIS.Application izin verilmeyen derlemeye başvuruyor: {ad}. "
                + "ApplicationIzinListesi'ne eklemek mimari karardır.",
            ad => $"K2 ölü istisna: SIS.Application artık {ad} derlemesine başvurmuyor; BilinenIhlaller.K2ApplicationBasvurulari'ndan düşün (liste yalnız küçülür)");
    }

    [Fact]
    public void K3_listede_olmayan_denetleyici_DbContexte_baglanmaz()
    {
        var bulunan = DbContexteBaglananDenetleyiciler();
        var bilinen = BilinenIhlaller.K3DbContexteBaglananDenetleyiciler.ToHashSet(StringComparer.Ordinal);

        var yeniler = bulunan.Where(p => !bilinen.Contains(p.Key))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"K3 yeni ihlal (CLAUDE.md:31 Controller DbContext kullanmaz): {p.Key} → {string.Join("; ", p.Value)}")
            .ToList();

        Assert.True(yeniler.Count == 0, string.Join(Environment.NewLine, yeniler));
    }

    [Fact]
    public void K3_bilinen_ihlaller_listesinde_olu_istisna_yok()
    {
        var bulunan = DbContexteBaglananDenetleyiciler();

        var oluler = BilinenIhlaller.K3DbContexteBaglananDenetleyiciler.Where(ad => !bulunan.ContainsKey(ad))
            .Select(ad => $"K3 ölü istisna: {ad} artık DbContext'e bağlı değil ya da böyle bir denetleyici yok; "
                + "BilinenIhlaller.K3DbContexteBaglananDenetleyiciler'den düşün (liste yalnız küçülür)")
            .ToList();

        Assert.True(oluler.Count == 0, string.Join(Environment.NewLine, oluler));
    }

    [Fact]
    public void Mimari_listeler_sirali_ve_tekrarsiz()
    {
        var listeler = new (string Ad, string[] Kayitlar)[]
        {
            (nameof(ApplicationIzinListesi), ApplicationIzinListesi),
            (nameof(BilinenIhlaller.K1DomainBasvurulari), BilinenIhlaller.K1DomainBasvurulari),
            (nameof(BilinenIhlaller.K2ApplicationBasvurulari), BilinenIhlaller.K2ApplicationBasvurulari),
            (nameof(BilinenIhlaller.K3DbContexteBaglananDenetleyiciler), BilinenIhlaller.K3DbContexteBaglananDenetleyiciler),
        };

        var hatalar = new List<string>();
        foreach (var (ad, kayitlar) in listeler)
        {
            for (var i = 1; i < kayitlar.Length; i++)
            {
                var fark = string.CompareOrdinal(kayitlar[i - 1], kayitlar[i]);
                if (fark == 0)
                    hatalar.Add($"{ad}: tekrar eden kayıt: {kayitlar[i]}");
                else if (fark > 0)
                    hatalar.Add($"{ad}: sıra bozuk (ordinal): {kayitlar[i - 1]} kaydı {kayitlar[i]} kaydından önce olamaz");
            }
        }

        Assert.True(hatalar.Count == 0, string.Join(Environment.NewLine, hatalar));
    }

    private static List<string> IzinsizBasvurular(Assembly derleme, IReadOnlyCollection<string> izinListesi) =>
        derleme.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(ad => !SistemDerlemesi(ad) && !izinListesi.Contains(ad, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static bool SistemDerlemesi(string ad) =>
        ad is "System" or "netstandard" or "mscorlib" || ad.StartsWith("System.", StringComparison.Ordinal);

    private static void BilinenlerleKarsilastir(
        IReadOnlyCollection<string> bulunan,
        IReadOnlyCollection<string> bilinen,
        Func<string, string> yeniMesaji,
        Func<string, string> oluMesaji)
    {
        var hatalar = bulunan.Where(ad => !bilinen.Contains(ad, StringComparer.Ordinal)).Select(yeniMesaji)
            .Concat(bilinen.Where(ad => !bulunan.Contains(ad, StringComparer.Ordinal)).Select(oluMesaji))
            .ToList();

        Assert.True(hatalar.Count == 0, string.Join(Environment.NewLine, hatalar));
    }

    /// <summary>
    /// API derlemesindeki soyut olmayan ControllerBase türevlerinden imzasında DbContext türevi olanlar.
    /// İç içe türler (derleyici üretimi kapanış ve durum makineleri dahil) ve ControllerBase'e kadar
    /// taban sınıflar, ait oldukları denetleyiciye atfedilir. Anahtar: denetleyicinin tam adı;
    /// değer: "DbContext türü (üye)" açıklamaları.
    /// </summary>
    private static Dictionary<string, List<string>> DbContexteBaglananDenetleyiciler()
    {
        var turler = ApiDerlemesi.GetTypes();
        var sonuc = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var denetleyici in turler.Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var incelenen = turler.Where(t => EnDistakiTur(t) == denetleyici)
                .Concat(TabanSiniflar(denetleyici));

            var bulgular = incelenen.SelectMany(ImzaTurleri)
                .SelectMany(p => AcilmisTurler(p.Tur).Where(t => typeof(DbContext).IsAssignableFrom(t))
                    .Select(t => $"{t.Name} ({p.Yer})"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            if (bulgular.Count > 0)
                sonuc[denetleyici.FullName ?? denetleyici.Name] = bulgular;
        }

        return sonuc;
    }

    private static Type EnDistakiTur(Type tur)
    {
        while (tur.DeclaringType is { } dis)
            tur = dis;
        return tur;
    }

    private static IEnumerable<Type> TabanSiniflar(Type tur)
    {
        for (var taban = tur.BaseType; taban is not null && taban != typeof(ControllerBase); taban = taban.BaseType)
            yield return taban;
    }

    private static IEnumerable<(string Yer, Type Tur)> ImzaTurleri(Type tur)
    {
        var ad = tur.Name;
        foreach (var f in tur.GetFields(TumUyeler))
            yield return ($"{ad} alanı {f.Name}", f.FieldType);
        foreach (var p in tur.GetProperties(TumUyeler))
            yield return ($"{ad} özelliği {p.Name}", p.PropertyType);
        foreach (var k in tur.GetConstructors(TumUyeler))
            foreach (var p in k.GetParameters())
                yield return ($"{ad} kurucu parametresi {p.Name}", p.ParameterType);
        foreach (var m in tur.GetMethods(TumUyeler))
        {
            yield return ($"{ad}.{m.Name} dönüş türü", m.ReturnType);
            foreach (var p in m.GetParameters())
                yield return ($"{ad}.{m.Name} parametresi {p.Name}", p.ParameterType);
        }
    }

    private static IEnumerable<Type> AcilmisTurler(Type tur)
    {
        if (tur.HasElementType && tur.GetElementType() is { } oge)
        {
            foreach (var t in AcilmisTurler(oge))
                yield return t;
            yield break;
        }

        yield return tur;
        if (tur.IsGenericType)
            foreach (var arguman in tur.GetGenericArguments())
                foreach (var t in AcilmisTurler(arguman))
                    yield return t;
    }
}
