# Duman testlerini yerelde Docker konteynerleriyle kosar (PowerShell, depo kokunden):
#   powershell -File SIS.Web/e2e-duman/calistir.ps1 [-AcikBirak] [playwright argumanlari]
# Konteynerler: obs-duman-db, obs-duman-api, obs-duman-web (ag obs-duman-net; kapilar 15173 ve 15213).
# Ilk kurulumda Development seed'i yaklasik 6 dakika surer. -AcikBirak konteynerleri sonraki kosu icin birakir.
param([switch]$AcikBirak, [Parameter(ValueFromRemainingArguments = $true)][string[]]$PwArgs)
$ErrorActionPreference = 'Stop'
$web = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repo = (Resolve-Path (Join-Path $web '..')).Path

function New-Rastgele([int]$n) {
  $b = New-Object byte[] $n
  [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
  return ([BitConverter]::ToString($b) -replace '-', '')
}
function Test-Calisiyor([string]$ad) { (docker ps -q --filter "name=^$ad$") -ne $null }
function Remove-Duman {
  foreach ($k in 'obs-duman-web', 'obs-duman-api', 'obs-duman-db') {
    if (docker ps -aq --filter "name=^$k$") { docker rm -f $k | Out-Null }
  }
  if (docker network ls -q --filter "name=^obs-duman-net$") { docker network rm obs-duman-net | Out-Null }
}
function Show-ApiGunluk {
  # Kurulum hatasinda konteyner silinmeden once: yalniz uyari/hata satirlari; "@", parola ve baglanti dizisi iceren satirlar haric.
  Write-Host 'obs-duman-api gunlugunun son uyari ve hata satirlari:'
  try {
    cmd /c "docker logs --tail 500 obs-duman-api 2>&1" |
      Where-Object { $_ -match '(?i)^(warn|fail|crit)|error|exception' -and $_ -notmatch '(?i)@|password|pwd|host=|server=|connectionstring|jwt' } |
      Select-Object -Last 20 | ForEach-Object { Write-Host $_ }
  } catch { Write-Host '(gunluk okunamadi)' }
}
function Wait-Adres([string]$url, [string]$konteyner, [int]$dakika) {
  $son = (Get-Date).AddMinutes($dakika)
  while ((Get-Date) -lt $son) {
    if (-not (Test-Calisiyor $konteyner)) { throw "$konteyner durdu (son uyari ve hata satirlari yukarida)" }
    try { if ((Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 5).StatusCode -eq 200) { return } } catch { }
    Start-Sleep -Seconds 5
  }
  throw "$url $dakika dakikada hazir olmadi (son uyari ve hata satirlari yukarida)"
}

if (-not (Test-Calisiyor 'obs-duman-api')) {
  Remove-Duman
  $dbParola = 'Db' + (New-Rastgele 16)
  docker network create obs-duman-net | Out-Null
  docker run -d --name obs-duman-db --network obs-duman-net --network-alias db --tmpfs /var/lib/postgresql `
    -e "POSTGRES_PASSWORD=$dbParola" -e POSTGRES_DB=sis postgres:18.7-trixie | Out-Null
  $apiKomut = 'mkdir /work && tar -C /src --exclude=node_modules --exclude=bin --exclude=obj --exclude=.git -cf - . | tar -C /work -xf - && cd /work && dotnet tool restore && dotnet build SIS.API/SIS.API.csproj -c Release && until dotnet ef database update -p SIS.Infrastructure -s SIS.API --no-build --configuration Release; do sleep 3; done && cd SIS.API && exec dotnet run --no-build -c Release --no-launch-profile'
  docker run -d --name obs-duman-api --network obs-duman-net -p 127.0.0.1:15213:5213 -p 127.0.0.1:15173:5173 `
    -v "${repo}:/src:ro" `
    -e "ConnectionStrings__DefaultConnection=Host=db;Port=5432;Database=sis;Username=postgres;Password=$dbParola" `
    -e 'Cors__AllowedOrigins__0=http://localhost:5173' -e ASPNETCORE_ENVIRONMENT=Development `
    -e "Jwt__Key=$(New-Rastgele 48)" -e Jwt__Issuer=sis-duman -e Jwt__Audience=sis-duman -e Jwt__ExpiresInMinutes=60 `
    -e ASPNETCORE_URLS=http://0.0.0.0:5213 mcr.microsoft.com/dotnet/sdk:10.0 sh -c $apiKomut | Out-Null
  $webKomut = 'mkdir /work && tar -C /src --exclude=node_modules --exclude=dist -cf - . | tar -C /work -xf - && cd /work && npm ci --no-audit --no-fund && npm run build && exec npx vite preview --host 0.0.0.0 --port 5173 --strictPort'
  docker run -d --name obs-duman-web --network container:obs-duman-api -v "${web}:/src:ro" node:20 sh -c $webKomut | Out-Null
}

try {
  Wait-Adres 'http://127.0.0.1:15213/api/auth/login-settings' 'obs-duman-api' 20
  Wait-Adres 'http://127.0.0.1:15173/login' 'obs-duman-web' 5
  $env:DUMAN_PAROLA = 'Aa1!' + (New-Rastgele 18)
  $env:DUMAN_ADRES = 'http://localhost:15173'
  $env:DUMAN_DB_KONTEYNER = 'obs-duman-db'
  Push-Location $web
  try { npx playwright test -c playwright.duman.config.ts @PwArgs; $kod = $LASTEXITCODE } finally { Pop-Location }
} finally {
  Remove-Item Env:DUMAN_PAROLA -ErrorAction SilentlyContinue
  if ($null -eq $kod) { Show-ApiGunluk }
  if (-not $AcikBirak) { Remove-Duman }
}
exit $kod
