@echo off
rem start.bat — поднимает весь проект (PostgreSQL + MinIO + бэкенд + фронтенд) через Docker.
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
  echo Перезапустите среду (или войдите заново), затем запустите start.bat снова.
  exit /b 0
)

if not exist .env (
  echo ==^> Создаю .env из .env.example
  copy .env.example .env >nul
  echo   Заполните SOURCECRAFT_PAT и YANDEX_CLIENT_ID/SECRET в .env
)

echo ==^> docker compose up --build -d
docker compose up --build -d
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo ==^> Готово:
echo   Frontend:      http://localhost:8080
echo   Backend API:   http://localhost:5172
echo   MinIO console: http://localhost:9001
endlocal
