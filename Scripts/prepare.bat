@echo off
chcp 866 >nul
rem prepare.bat - интерактивная подготовка и запуск проекта (Windows).
rem
rem Что делает:
rem   1) спрашивает нужные для СТАРТА токены и объясняет, где их взять;
rem   2) генерирует ключ шифрования и создаёт .env;
rem   3) запускает проект (Scripts\start.bat - поставит Docker при отсутствии).
setlocal enabledelayedexpansion

cd /d "%~dp0.."

echo === SourceCraft Repo Health Checker - подготовка ===
echo.
echo Нужны следующие данные:
echo.
echo 1) Я ID (обязательно) - вход пользователей через Яндекс.
echo    Где взять: https://oauth.yandex.ru/client/new
echo      - Платформа: Веб-сервисы
echo      - Redirect URI: http://localhost:8080/auth/callback
echo      - Доступы (scope): login:info, login:email
echo    Заберите ClientID и ClientSecret.
echo.
echo 2) SourceCraft PAT (необязательно) - сервисный токен для публичного рейтинга
echo    всех открытых репозиториев.
echo    Где взять: https://sourcecraft.dev - Домой - Доступ - Персональные токены доступа - Сгенерировать.
echo    ВАЖНО: это сервисный токен. Свой PAT для личных/приватных репозиториев
echo    пользователь вводит позже на сайте в разделе "Настройки".
echo.

set /p YANDEX_CLIENT_ID="Я ID ClientID: "
set /p YANDEX_CLIENT_SECRET="Я ID ClientSecret: "
set /p SOURCECRAFT_PAT="SourceCraft PAT (Enter - пропустить): "

if "%YANDEX_CLIENT_ID%"=="" (
  echo Ошибка: Я ID ClientID/ClientSecret обязательны для входа.
  exit /b 1
)
if "%YANDEX_CLIENT_SECRET%"=="" (
  echo Ошибка: Я ID ClientID/ClientSecret обязательны для входа.
  exit /b 1
)

for /f "usebackq delims=" %%K in (`powershell -NoProfile -Command "[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))"`) do set ENCRYPTION_KEY=%%K

(
  echo # Создано prepare.bat
  echo POSTGRES_PASSWORD=postgres
  echo AI_TOKEN_ENCRYPTION_KEY=%ENCRYPTION_KEY%
  echo SOURCECRAFT_PAT=%SOURCECRAFT_PAT%
  echo YANDEX_CLIENT_ID=%YANDEX_CLIENT_ID%
  echo YANDEX_CLIENT_SECRET=%YANDEX_CLIENT_SECRET%
  echo YANDEX_REDIRECT_URI=http://localhost:8080/auth/callback
) > .env

echo .env создан ^(AI_TOKEN_ENCRYPTION_KEY сгенерирован^).
echo Запускаю проект: Scripts\start.bat
call Scripts\start.bat
endlocal