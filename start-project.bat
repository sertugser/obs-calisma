@echo off
setlocal

pushd "%~dp0"

echo =========================================
echo SIS Project - Starting in Integrated Terminal...
echo =========================================

set "PATH=%PATH%;C:\Program Files\nodejs;%AppData%\npm"
set "DOTNET_CLI_HOME=%CD%\.dotnet-cli"
set "MSBUILDDISABLENODEREUSE=1"

echo.
echo [0/3] Cleaning up stale backend processes...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$repo=(Resolve-Path '.').Path; Get-Process -Name SIS.API -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($repo, [System.StringComparison]::OrdinalIgnoreCase) } | Stop-Process -Force"
timeout /t 2 /nobreak >nul

echo.
echo [1/3] Backend (SIS.API) setup...
cd SIS.API
call dotnet restore
call dotnet build -p:UseSharedCompilation=false
cd ..

echo.
echo [1.5/3] Database migration...
call dotnet tool restore
if errorlevel 1 (
  echo [WARN] dotnet tool restore failed. Skipping database update and continuing startup...
) else (
  rem Kurulum: migration, sistem verisi, ilk yonetici, yerel kurum verisi ve ornek veri.
  rem Yonetici icin SIS_SETUP_ADMIN_EMAIL ve SIS_SETUP_ADMIN_PASSWORD, ornek hesaplar icin SIS_DEMO_PASSWORD gerekir.
  cd SIS.API
  call dotnet run --no-build --launch-profile http -- setup --kurum yerel --seed-demo
  if errorlevel 1 (
    echo [WARN] Setup failed. Bos veritabaninda SIS_SETUP_ADMIN_EMAIL ve SIS_SETUP_ADMIN_PASSWORD gerekir; ornek hesaplar icin SIS_DEMO_PASSWORD. Uygulama verisiz acilacak.
  )
  cd ..
)

echo.
echo [2/3] Frontend (SIS.Web) setup...
cd SIS.Web
call npm install
cd ..

echo.
echo =========================================
echo Preparation complete! Starting projects...
echo Both API and Web outputs will be shown here.
echo (Press CTRL+C once to stop both later)
echo =========================================

:: Start Backend in background (same terminal window)
:: The project was already built above, so avoid a second build here. On slower
:: machines that extra build can make the readiness check report a false warning.
start /b cmd /c "cd SIS.API && set DOTNET_CLI_HOME=%DOTNET_CLI_HOME% && dotnet run --no-build --launch-profile http"

:: Start Frontend in background (same terminal window)
start /b cmd /c "cd SIS.Web && npm run dev"

echo.
echo Waiting for servers to be ready...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(60); while ((Get-Date) -lt $deadline) { $tcp=$null; try { $tcp=[Net.Sockets.TcpClient]::new(); $connect=$tcp.BeginConnect('127.0.0.1',5213,$null,$null); if ($connect.AsyncWaitHandle.WaitOne(500)) { $tcp.EndConnect($connect); $tcp.Close(); exit 0 } } catch { } finally { if ($tcp) { $tcp.Close() } }; Start-Sleep -Milliseconds 500 }; exit 1" >nul 2>&1

if errorlevel 1 (
  echo [WARN] Backend API 5213 portunda acilmadi.
  echo [WARN] Bu durumda en olasi neden Windows App Control / Smart App Control / AppLocker bloklamasidir.
  echo [WARN] Frontend acilabilir ama /api istekleri calismayacaktir.
) else (
  echo [OK] Backend API 5213 portunda ayakta.
)

echo Opening browser: http://localhost:5173/
start http://localhost:5173/

echo.
echo =========================================
echo Startup sequence finished.
echo =========================================

popd
