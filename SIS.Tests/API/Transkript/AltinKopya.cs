using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SIS.Tests.API.Transkript;

/// <summary>
/// Altın kopya yardımcısı (D-06): çıktıları "Yol = değer" satırlarına düzleştirir ve kaynak klasördeki dosyayla
/// satır satır karşılaştırır. Dosyaya yazan bir yol yoktur; ilk üretim başarısız testin çıktısından elle yapılır.
/// </summary>
internal sealed class AltinKopya(IReadOnlyDictionary<int, string> dersKodlari)
{
    // Alan adları DTO'daki gibi; enum'lar okunur olsun diye metin. API'nin JSON ayarından bağımsız.
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
    // Belirlenimsiz (üretim zamanı) ve sağlayıcının ürettiği kimlik alanları çıkarılır.
    private static readonly HashSet<string> Cikarilan = ["GeneratedAt", "StudentId", "RegistrationId", "CourseId", "TermId"];
    private const int AzamiFark = 20;
    private readonly List<string> _satirlar = [];

    public string Metin => string.Join("\n", _satirlar);

    public void Ekle(string kok, object? deger) => Yaz(kok, JsonSerializer.SerializeToElement(deger, Json));

    private void Yaz(string yol, JsonElement deger)
    {
        switch (deger.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var alan in deger.EnumerateObject().Where(a => !Cikarilan.Contains(a.Name)))
                {
                    if (alan.Name == "EquivalentCourseIds") // kimlikler ders koduna çevrilir
                        _satirlar.Add($"{yol}.{alan.Name} = [{string.Join(", ", alan.Value.EnumerateArray()
                            .Select(id => dersKodlari[id.GetInt32()]).Order(StringComparer.Ordinal))}]");
                    else
                        Yaz($"{yol}.{alan.Name}", alan.Value);
                }
                break;
            case JsonValueKind.Array:
                if (deger.GetArrayLength() == 0)
                    _satirlar.Add($"{yol} = []");
                var sira = 0;
                foreach (var oge in deger.EnumerateArray())
                    Yaz($"{yol}[{Anahtar(oge, sira++)}]", oge);
                break;
            case JsonValueKind.String:
                _satirlar.Add($"{yol} = {deger.GetString()}");
                break;
            default: // sayı, true/false, null: JSON metni (kültürden bağımsız, decimal ölçeği korunur)
                _satirlar.Add($"{yol} = {deger.GetRawText()}");
                break;
        }
    }

    // Dizi öğesi ders koduyla ya da dönem adıyla anahtarlanır; ikisi de yoksa sıra numarasıyla.
    private static string Anahtar(JsonElement oge, int sira) =>
        oge.ValueKind == JsonValueKind.Object
        && (oge.TryGetProperty("CourseCode", out var anahtar) || oge.TryGetProperty("TermName", out anahtar))
            ? anahtar.GetString()!
            : sira.ToString(CultureInfo.InvariantCulture);

    public void Karsilastir(string dosyaAdi, [CallerFilePath] string kaynakDosya = "")
    {
        var yol = Path.Combine(Path.GetDirectoryName(kaynakDosya)!, dosyaAdi);
        if (!File.Exists(yol))
            Assert.Fail($"Altın kopya yok: {yol}\nGerçek çıktı ({_satirlar.Count} satır):\n{Metin}");

        var beklenen = File.ReadAllText(yol, Encoding.UTF8).Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var farklar = new List<string>();
        for (var i = 0; i < Math.Max(beklenen.Length, _satirlar.Count); i++)
        {
            var b = i < beklenen.Length ? beklenen[i] : "(yok)";
            var g = i < _satirlar.Count ? _satirlar[i] : "(yok)";
            if (b != g)
                farklar.Add($"satır {i + 1}: beklenen {b} | gerçek {g}");
        }

        if (farklar.Count > 0)
            Assert.Fail($"Altın kopyadan {farklar.Count} satır farklı (beklenen {beklenen.Length}, gerçek {_satirlar.Count} satır); "
                + $"ilk {Math.Min(AzamiFark, farklar.Count)}:\n{string.Join("\n", farklar.Take(AzamiFark))}");
    }
}
