#!/usr/bin/env bash
# prepare.sh — интерактивная подготовка и запуск проекта.
#
# Что делает:
#   1) спрашивает нужные для СТАРТА токены и объясняет, где их взять;
#   2) генерирует ключ шифрования и создаёт .env;
#   3) запускает проект (Scripts/start.sh — поднимет Docker при отсутствии).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
have() { command -v "$1" >/dev/null 2>&1; }

cat <<'INTRO'
=== SourceCraft Repo Health Checker — подготовка ===

Нужны следующие данные:

1) Я ID (обязательно) — вход пользователей через Яндекс.
   Где взять: https://oauth.yandex.ru/client/new
     • Платформа: «Веб-сервисы»
     • Redirect URI: http://localhost:8080/auth/callback
     • Доступы (scope): login:info, login:email
   Заберите ClientID и ClientSecret.

2) SourceCraft PAT (необязательно) — сервисный токен для публичного рейтинга
   всех открытых репозиториев.
   Где взять: https://sourcecraft.dev → Домой → Доступ → Персональные токены доступа → Сгенерировать.
   ВАЖНО: это сервисный токен. Свой PAT для анализа личных/приватных репозиториев
   пользователь вводит позже уже на сайте в разделе «Настройки».

Если оставить PAT пустым — публичный рейтинг наполнится только после того,
как пользователи проанализируют свои репозитории.

INTRO

read -r -p "Я ID ClientID: " YANDEX_CLIENT_ID
read -r -p "Я ID ClientSecret: " YANDEX_CLIENT_SECRET
read -r -p "SourceCraft PAT (Enter — пропустить): " SOURCECRAFT_PAT

if [ -z "${YANDEX_CLIENT_ID}" ] || [ -z "${YANDEX_CLIENT_SECRET}" ]; then
  echo "Ошибка: Я ID ClientID/ClientSecret обязательны для входа." >&2
  exit 1
fi

if have openssl; then
  ENCRYPTION_KEY="$(openssl rand -base64 32)"
else
  ENCRYPTION_KEY="$(head -c 32 /dev/urandom | base64 | tr -d '\n')"
fi

cat > .env <<EOF
# Создано prepare.sh
POSTGRES_PASSWORD=postgres
AI_TOKEN_ENCRYPTION_KEY=${ENCRYPTION_KEY}
SOURCECRAFT_PAT=${SOURCECRAFT_PAT}
YANDEX_CLIENT_ID=${YANDEX_CLIENT_ID}
YANDEX_CLIENT_SECRET=${YANDEX_CLIENT_SECRET}
YANDEX_REDIRECT_URI=http://localhost:8080/auth/callback
EOF

log ".env создан (AI_TOKEN_ENCRYPTION_KEY сгенерирован)."
log "Запускаю проект: bash Scripts/start.sh"
exec bash Scripts/start.sh
