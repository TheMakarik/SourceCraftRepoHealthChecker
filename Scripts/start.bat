@echo off
chcp 65001 >nul
rem start.bat — поднимает весь проект (PostgreSQL + бэкенд + фронтенд) через Docker.
rem Если Docker не установлен — пытается установить Docker Desktop через winget/choco.
setlocal

cd /d "%~dp0.."

where docker >nul 2>nul
if not %errorlevel%==0 (
  echo ==^> Docker не найден — устанавливаю Docker Desktop
  where winget >nul 2>nul
  if %errorlevel%==0 (
    winget install --id Docker.DockerDesktop -e --accept-source-agreements --accept-package-agreements
  ) else (
    where choco >nul 2>nul
    if %errorlevel%==0 (
      choco install -y docker-desktop
    ) else (
      echo Установите Docker вручную: https://docs.docker.com/desktop/install/windows-install/
      exit /b 1
    )
  )
  echo Перезапустите среду ^(или войдите заново^), затем запустите start.bat снова.
  exit /b 0
)

if not exist .env (
  echo ==^> Создаю .env из .env.example
  copy .env.example .env >nul
  where powershell >nul 2>nul
  if %errorlevel%==0 (
    powershell -NoProfile -Command "$rng=[System.Security.Cryptography.RandomNumberGenerator]::Create(); $b=[byte[]]::new(32); $rng.GetBytes($b); $key=[Convert]::ToBase64String($b); $envPath=Join-Path $PWD '.env'; $content=(Get-Content -Raw $envPath) -replace '(?m)^AI_TOKEN_ENCRYPTION_KEY=.*$', ('AI_TOKEN_ENCRYPTION_KEY=' + $key); [System.IO.File]::WriteAllText($envPath, $content)"
  ) else (
    echo   Внимание: PowerShell не найден — задайте AI_TOKEN_ENCRYPTION_KEY в .env вручную.
  )
  echo   Заполните SOURCECRAFT_PAT и YANDEX_CLIENT_ID/SECRET в .env
)

echo ==^> docker compose up --build -d
docker compose up --build -d
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo ==^> Готово:
echo   Frontend:      http://localhost:8080
echo   Backend API:   http://localhost:5172
endlocal
