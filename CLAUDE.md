# Ankara Bilim Üniversitesi — Öğrenci Bilgi Sistemi (SIS)

## 1. Bu dosya ne
- Bu bir kural dosyasıdır: kural, komut ve kısa proje haritası. Tarihçe içermez.
- Çelişkide iş paketi metni geçerlidir.
- Eski özellik günlüğü: `docs/history/claude-md-gunluk.md` (kural değil, tarihçe).

## 2. Proje haritası
- Çözüm: `StudentInformationSystem.slnx` (kökte). Tüm projeler `net10.0`.
- `SIS.Domain/` — `Entities/`, `Enums/`, `ValueObjects/`, `Exceptions/`, `Identity/`. Hiçbir paket referansı yok.
- `SIS.Application/` — servis arayüzleri, DTO, validator (FluentValidation). Referans: Domain.
- `SIS.Infrastructure/` — EF Core + Npgsql (PostgreSQL), `Persistence/` (DbContext, `ApplicationUser.cs`, `Seeds/`), `Migrations/`, `Services/`. Referans: Domain, Application.
- `SIS.API/` — `Controllers/`, `Authorization/`, `Program.cs`, JWT Bearer. Referans: Application, Infrastructure.
- `SIS.Tests/` — xUnit. Referans: API, Application, Domain, Infrastructure.
- `SIS.Web/` — React + TypeScript + Vite + TanStack Query. Kaynak: `src/{api,components,context,hooks,lib,pages,types}`.
- `scripts/` — tek seferlik SQL ve yardımcı betikler. `docs/CODEMAPS/` — kod haritaları.
- `.config/dotnet-tools.json` — `dotnet-ef` yerel aracı.

Komutlar:
```bash
dotnet build                                                  # kökten, tüm çözüm
dotnet tool restore
dotnet ef database update -p SIS.Infrastructure -s SIS.API    # migration uygula
cd SIS.API && dotnet run --launch-profile http                # API, http://localhost:5213
cd SIS.Web && npm ci && npm run dev                           # frontend, http://localhost:5173
start-project.bat                                             # hepsini sırayla başlatır (Windows)
```

## 3. Katman kuralları
- İş kuralı Domain'dedir. Application yalnız orkestrasyon yapar. Infrastructure yalnız teknik iş yapar.
  Controller incedir ve DbContext kullanmaz.
- Domain entity'leri çatıdan bağımsızdır ama davranış taşır. "Plain C# classes" yalnız "hiçbir
  çatıya bağlı değil" demektir; "davranış içermez" demek değildir (örn. `AcademicTerm.GetStatus`).
- Yeni kural yazmadan önce mevcut tanım aranır ve o kullanılır; kopya yasaktır. Bugünkü örnekler:
  `RelativeGradeOutcome` (SIS.Application/Grading/RelativeGradingService.cs:60),
  `PrerequisiteEvaluator` (SIS.Infrastructure/Services/PrerequisiteEvaluator.cs:12).
- Frontend karar vermez; hesap gerekiyorsa backend ucu yazılır.
- Kuruma özgü sayı koda yazılmaz; yapılandırmadan ya da politikadan gelir.
- Yeni alan, onu kullanan kural ve testle aynı PR'da gelir.
- Yetkiyi kaldıran ya da gevşeten değişiklik ayrı PR olur; birleştirilmeden önce ayrı bir oturumda
  güvenlik incelemesi yapılır.
- "Sil" ya da "kaldır" içeren her değişiklik PR'da kullanım aramasının çıktısını gösterir.

Bugünkü durum (bilinen borç; örnek alınmaz, yeni kod yukarıdaki kurallara uyar):
- `SIS.API/Controllers/` altındaki 74 controller'ın 52'si `AppDbContext`'i doğrudan kullanıyor.
- Not hesabı `GradeController`'da (ağırlıklı ortalama controller içinde hesaplanıyor).
- İş kuralları `SIS.Infrastructure/Services/` altında da duruyor (örn. `SeatLedger`,
  `PrerequisiteEvaluator`). `PrerequisiteEvaluator` Infrastructure'da `internal static` bir sınıf;
  Domain'de olmalı.
- Mimari testler henüz yok.

## 4. Çalışma kuralları
- Bir oturum, bir iş paketi; paketin kapsamı dışına çıkılmaz.
- Kanıt kuralı: her iddia dosya:satır ya da komut çıktısıyla verilir; kanıtsız iddia
  "doğrulanmadı" diye işaretlenir.
- Çalıştır, varsayma: derleme ve testler gerçekten çalıştırılır.
- Önce envanter: değişiklikten önce ilgili kuralın tüm mevcut kopyaları arama çıktısıyla listelenir.
- Durma koşulları (iş bırakılır ve sorulur):
  - yönetmelik yorumu gerekiyorsa;
  - bir testi geçirmek için testin kendisini değiştirmek gerekiyorsa;
  - kullanımı olan kod silinecekse;
  - kapsam dışı bir dosyaya dokunmak gerekiyorsa;
  - değişiklik yaklaşık 400 satırı aşacaksa (paket bölünür);
  - bir iddia kodla ya da komut çıktısıyla doğrulanamıyorsa.

## 5. Varsayılan dokunulmaz liste
Paket açıkça bunlarla ilgili değilse dokunulmaz:
- Uygulanmış migration'lar ve `AppDbContextModelSnapshot`.
- `SIS.API/Authorization/` ve yetki kuralları.
- Ders programı motoru, OR-Tools çözücüsü ve parite testleri.
- `SeatLedger`.
- Mevcut testlerin beklenen değerleri.
- CI iş akışları (`.github/workflows/`) ve `.claude/` ayarları.
- `appsettings*.json` içindeki anahtarlar ve `.env` dosyaları: okunmaz da.
- `docs/analysis/` ve `docs/plan/`: Git dışında; içerikleri commit'e, PR'a ya da bu dosyaya alıntılanmaz.
- Bu dosyanın kendisi (`CLAUDE.md`): yalnız insan onayıyla, bir iş paketiyle değişir.

## 6. Test ve doğrulama
- Bu bilgisayarda Windows Smart App Control derlenen test DLL'lerini engelliyor (0x800711C7);
  `dotnet test` yerelde çalışmaz. Smart App Control kapatılmaz. Testler Docker içinde, Linux'ta
  çalıştırılır (PowerShell'de, repo kökünden; Git Bash `-v` yolunu bozar):
  ```powershell
  docker run --rm -v "${PWD}:/src:ro" mcr.microsoft.com/dotnet/sdk:10.0 sh -c "mkdir /work && tar -C /src --exclude=node_modules --exclude=bin --exclude=obj --exclude=.git -cf - . | tar -C /work -xf - && cd /work && dotnet test"
  ```
  Klasör salt okunur bağlanır ve konteynere kopyalanır; böylece Windows'taki `bin/` ve `obj/` bozulmaz.
- Test taban çizgisi: 1.483 test; 1.481 geçti, 0 kaldı, 2 atlandı. Her değişiklik bununla karşılaştırılır.
- nuget-license da aynı nedenle Docker içinde çalıştırılır.
- Frontend (`SIS.Web/` içinde): `npm ci`, `npm run lint`, `npx tsc --noEmit`, `npm run build`.

## 7. Git ve PR
- `origin`: sertugser/obs-calisma (private). `main` korumalı: doğrudan push yok, PR zorunlu;
  zorunlu kontroller: Backend Build & Test, Frontend Build & Lint, Security Audit.
- `eski` remote'u (sertugser/abu-sis) arşivlenmiştir; ona push yapılmaz.
- PR açıklaması 3–5 satırlık bir nottur: hangi iş paketi, ne değişti, nasıl doğrulandı (komut ve
  çıktı), ne doğrulanamadı.
- PR'da şu dört soru cevaplanır: Hangi katmana dokundu? Mevcut bir kuralı kopyaladı mı? Testi
  nerede? Yetki etkisi var mı?
- Dosyalar `git add .` ile değil, adıyla eklenir.

## 8. Yapay zekâ ibaresi yasağı
Commit mesajlarında, PR'larda, kodda ve dokümanlarda yapay zekâyla yapıldığına dair hiçbir ibare
yer almaz (Co-Authored-By, "Generated with", oturum satırı vb.). Attribution ayarı kapalıdır;
`.git/hooks/commit-msg` kalan ibareleri siler. Kancaya güvenip ibare ekleme.
