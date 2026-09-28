@echo off
rem setup-deps.bat — установка зависимостей для разработки и запуска тестов бэкенда (Windows).
rem
rem ЧТО ДЕЛАЕТ:h
rem   Ставит .NET SDK 10 (плюс git), затем печатает версии для проверки.
rem   Скрипт идемпотентный: повторный запуск ничего не ломает.
rem
rem ЧЕМ СТАВИТ (менеджеры пакетов):
rem   * winget — основной способ (Windows 10/11);
rem   * choco  — если winget недоступен, но установлен Chocolatey;
rem   * dotnet-install.ps1 — официальный установщик Microsoft, если SDK нет ни в winget, ни в choco.
rem
rem ОТКУДА КАЧАЕТ:
rem   * winget: Git.Git, Microsoft.DotNet.SDK.10 (магазин winget / репозитории вендоров);
rem   * choco:  git, dotnet-sdk (community-репозиторий chocolatey.org);
rem   * .NET:  официальный установщик Microsoft https://dot.net/v1/dotnet-install.ps1 (channel 10.0).
rem
rem ФРОНТЕНД:
rem   Скрипт пока НЕ ставит Node.js и пакетный менеджер для фронтенда — это нужно будет добавить
rem   позже (см. Skills/frontend-start).
setlocal enabledelayedexpansion

set DOTNET_CHANNEL=10.0

where winget >nul 2>nul
if %errorlevel%==0 (
  echo ==^> winget: Git.Git Microsoft.DotNet.SDK.10
  winget install --id Git.Git -e --accept-source-agreements --accept-package-agreements
  winget install --id Microsoft.DotNet.SDK.10 -e --accept-source-agreements --accept-package-agreements
  goto verify
)

where choco >nul 2>nul
if %errorlevel%==0 (
  echo ==^> choco: git dotnet-sdk
  choco install -y git dotnet-sdk
  goto verify
)

echo ==^> winget и choco не найдены. Ставлю .NET официальным установщиком от Microsoft.
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Invoke-WebRequest -UseBasicParsing https://dot.net/v1/dotnet-install.ps1 -OutFile $env:TEMP\dotnet-install.ps1; & $env:TEMP\dotnet-install.ps1 -Channel %DOTNET_CHANNEL%"

:verify
echo.
echo ==^> Готово. Версии:
where dotnet >nul 2>nul && dotnet --version || echo   dotnet: не найден (перезапустите терминал)
endlocal
