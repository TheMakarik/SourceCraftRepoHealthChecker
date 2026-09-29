#!/usr/bin/env bash
# start.sh — поднимает весь проект (PostgreSQL + бэкенд + фронтенд) через Docker.
# Если Docker не установлен — пытается установить его автоматически.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
have() { command -v "$1" >/dev/null 2>&1; }

install_docker() {
  log "Docker не найден — устанавливаю (get.docker.com)"
  if ! have curl; then
    echo "Нужен curl для установки Docker." >&2
    exit 1
  fi
  curl -fsSL https://get.docker.com | sh
  if have systemctl; then
    systemctl enable --now docker || true
  fi
  if [ "$(id -u)" -ne 0 ] && have usermod; then
    usermod -aG docker "$USER" 2>/dev/null || true
  fi
  log "Docker установлен. Возможно, потребуется перелогиниться (группа docker)."
}

if ! have docker; then
  install_docker
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "Не найден плагин 'docker compose'. Установите docker-compose-plugin." >&2
  exit 1
fi

SUDO=""
if [ "$(id -u)" -ne 0 ] && ! docker info >/dev/null 2>&1; then
  if have sudo; then
    SUDO="sudo"
  else
    echo "Нет прав на Docker: запустите от root или добавьте пользователя в группу docker." >&2
    exit 1
  fi
fi

if [ ! -f .env ]; then
  log "Создаю .env из .env.example"
  cp .env.example .env
  if have openssl; then
    key="$(openssl rand -base64 32)"
    if have sed; then
      sed -i "s|^AI_TOKEN_ENCRYPTION_KEY=.*|AI_TOKEN_ENCRYPTION_KEY=${key}|" .env
    fi
  fi
  echo "  Заполните SOURCECRAFT_PAT и YANDEX_CLIENT_ID/SECRET в .env (для приватных репо и входа)."
fi

log "Сборка и запуск: docker compose up --build -d"
$SUDO docker compose up --build -d

log "Готово. Открывайте:"
echo "  Frontend:      http://localhost:8080"
echo "  Backend API:   http://localhost:5172"
echo
echo "Логи: $SUDO docker compose logs -f backend"
