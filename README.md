# Student Information System (SIS)

Ankara Bilim University - Student Information System

## Kullanılan Teknolojiler

| Kategori | Teknolojiler |
|----------|-------------|
| **Backend** | ![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white) ![EF Core](https://img.shields.io/badge/EF_Core_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white) |
| **Frontend** | ![React](https://img.shields.io/badge/React-20232A?style=for-the-badge&logo=react&logoColor=61DAFB) ![TypeScript](https://img.shields.io/badge/TypeScript-007ACC?style=for-the-badge&logo=typescript&logoColor=white) ![Vite](https://img.shields.io/badge/Vite-646CFF?style=for-the-badge&logo=vite&logoColor=white) ![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-38B2AC?style=for-the-badge&logo=tailwind-css&logoColor=white) |
| **Veritabanı** | ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white) |
| **Auth** | ![JWT](https://img.shields.io/badge/JWT-000000?style=for-the-badge&logo=jsonwebtokens&logoColor=white) |

## Ön-Koşullar

Aşağıdakiler **kurulu ve çalışır** olmalı:

| Araç | Versiyon |
|------|----------|
| .NET SDK | 10.0+ |
| Node.js | 18+ |
| PostgreSQL | 18 (servis çalışır durumda) |

## Kurulum

### 1. Repo'yu klonla

```bash
git clone --recurse-submodules <repo-url>
cd Student-Information-System
```

> **Not:** `--recurse-submodules` flag'i `external/AISVS/` submodule'ünü de çeker. Düz `git clone` yaptıysan sonradan `git submodule update --init --recursive` çalıştır.

### 2. PostgreSQL veritabanını oluştur

```sql
CREATE DATABASE "StudentInfoSystem";
```

### 3. Backend ayarları

Development secret değerlerini repoya yazmayın. `SIS.API/appsettings.Development.example.json`
yalnızca güvenli örnek dosyadır; gerçek connection string ve JWT key için ASP.NET User Secrets
veya environment variable kullanın:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=StudentInfoSystem;Username=postgres;Password=<strong-password>" --project SIS.API
dotnet user-secrets set "Jwt:Key" "<at-least-32-byte-random-secret>" --project SIS.API
```

Environment variable alternatifi:

```bash
set ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=StudentInfoSystem;Username=postgres;Password=<strong-password>
set Jwt__Key=<at-least-32-byte-random-secret>
```

Alternatif olarak `SIS.API/appsettings.Development.json` dosyasını oluşturabilirsiniz (gitignored, sadece sende kalır):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=StudentInfoSystem;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Jwt": {
    "Key": "your-secret-key-must-be-at-least-32-characters-long",
    "Issuer": "StudentInformationSystem",
    "Audience": "StudentInformationSystem",
    "ExpiresInMinutes": 60
  }
}
```

> **JWT Key minimum 32 karakter** olmalı. Aksi takdirde API açılışta `StartupSecurityValidator` tarafından reddedilir ve başlatılamaz.

### 4. Veritabanı migration'larını uygula

```bash
dotnet ef database update -p SIS.Infrastructure -s SIS.API
```

Ya da kurulum komutuyla: migration'ları uygular, sistem verisini (roller, izinler, menü, ayarlar) yazar ve ilk yöneticiyi oluşturur. Tekrar çalıştırılması güvenlidir; yönetici varsa dokunmaz.

```bash
cd SIS.API
SIS_SETUP_ADMIN_EMAIL=yonetici@example.edu SIS_SETUP_ADMIN_PASSWORD='<güçlü parola>' dotnet run -- setup
```

Kurumun akademik yapısı (fakülte, bölüm, program, ders, derslik ve kuruma özel açma nedenleri) `--kurum yerel` ile yazılır; çekirdek bu veriyi içermez. Örnek veri de istenirse sona `--seed-demo` eklenir (örnek veri bugün yerel kurumun yapısını kullanır ve onu da yazar). Varsayılan yönetici parolası yoktur; parola değişkeni boşsa ve yönetici yoksa komut durur.

- Kurulum, sunucuyla aynı yapılandırmayı ister (bağlantı cümlesi, JWT ayarları, CORS, AllowedHosts); bunlar ortam değişkeniyle ya da `appsettings` ile verilir, komut satırı ayarları okunmaz.
- `--seed-demo` bugün sabit parolalı örnek hesaplar (aşağıdaki tablo) da oluşturur; yalnız geliştirme veritabanında kullanılmalıdır.

### 5. Backend'i başlat

```bash
cd SIS.API
dotnet run
```

API varsayılan olarak `http://localhost:5213` adresinde çalışır.

### 6. Frontend bağımlılıklarını yükle ve başlat

```bash
cd SIS.Web
npm install
npm run dev
```

## Hızlı başlangıç (Windows)

Adım 4-6 yerine tek komutla başlatabilirsin:

```cmd
start-project.bat
```

Bu script `dotnet restore`, `dotnet build`, EF Core migration, `npm install` ve hem backend (port 5213) hem frontend'i (port 5173) çalıştırır, sonra tarayıcıyı açar.

> **Önemli:** `start-project.bat` PostgreSQL'i kurmaz, veritabanını oluşturmaz ve `appsettings.Development.json` yaratmaz. Adım 2 ve 3 önceden tamamlanmış olmalıdır.

## Varsayılan Kullanıcılar (Development)

Örnek verideki bütün hesapların parolası `SIS_DEMO_PASSWORD` ortam değişkeninden gelir; değişken boşsa her çalışmada rastgele üretilir ve hiçbir yere yazılmaz. Kodda sabit parola yoktur.

| Rol | E-posta |
|-----|---------|
| Yönetici | yonetici@example.edu |
| İdari personel | idari@example.edu |
| Danışman | danisman@example.edu |
| Öğretim elemanı | ogretim01@example.edu … ogretim36@example.edu |
| Öğrenci | ornek01@example.edu … ornek18@example.edu, ogrenci0019@example.edu … |

## Proje Yapısı

```
SIS.Domain/           → Entities, Enums, ValueObjects (sıfır bağımlılık)
SIS.Application/      → Services, DTOs, Validators
SIS.Infrastructure/   → EF Core, DbContext, Repositories
SIS.API/              → Controllers, Middleware, DI
SIS.Web/              → React frontend (Vite + TypeScript + Tailwind)
SIS.Tests/            → Unit testler
scripts/              → Manuel SQL migration ve yetki yamaları
```

## Komutlar

```bash
dotnet build                          # Tüm çözümü derle
dotnet test                           # Testleri çalıştır
dotnet run --project SIS.API          # Backend'i başlat
cd SIS.Web && npm run dev             # Frontend'i başlat
```

CI'daki `Backend Coverage` işi yalnız main'e push'ta ve elle (workflow_dispatch) koşar, pull request'te koşmaz; kapsam özetini iş özetine yazar; zorunlu kontrol değildir, eşik yoktur.
EF migration dosyaları kapsam hesabına girmez.

## Hata giderme

**API başlatılamıyor / port 5213 açılmıyor:**

Terminal çıktısında `StartupSecurityValidator` hatasını ara. Genelde sebep:

- `appsettings.Development.json` yok veya eksik (Adım 3'e dön)
- `Jwt:Key` 32 karakterden kısa
- `ConnectionStrings:DefaultConnection` boş veya PostgreSQL erişilemez
- PostgreSQL servisi çalışmıyor

`start-project.bat` çalıştırırken "Smart App Control bloklaması" uyarısı görsen bile gerçek sebep büyük ihtimalle yukarıdakilerden biridir.
