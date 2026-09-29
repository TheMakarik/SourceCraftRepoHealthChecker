#!/usr/bin/env bash
# start.sh — поднимает весь проект (PostgreSQL + бэкенд + фронтенд) через Docker.
# Если Docker не установлен — пытается установить его автоматически
# (Linux: get.docker.com; macOS: Homebrew → Colima, без brew — ссылка на Docker Desktop).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
have() { command -v "$1" >/dev/null 2>&1; }

install_docker_linux() {
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

# macOS: get.docker.com там не работает. Ставим через Homebrew Colima (Docker Engine в лёгкой ВМ,
# без sudo и GUI) + docker CLI с плагинами compose/buildx. Без Homebrew — прямая ссылка на Docker Desktop.
docker_desktop_url() {
  if [ "$(uname -m)" = "arm64" ]; then
    echo "https://desktop.docker.com/mac/main/arm64/Docker.dmg"
  else
    echo "https://desktop.docker.com/mac/main/amd64/Docker.dmg"
  fi
}

install_docker_macos() {
  if ! have brew; then
    echo "Docker не найден, Homebrew тоже. Варианты:" >&2
    echo "  1) Docker Desktop: $(docker_desktop_url) — установить, запустить и повторить скрипт;" >&2
    echo "  2) Homebrew (https://brew.sh), затем повторить скрипт — Docker поставится автоматически." >&2
    exit 1
  fi
  log "Docker не найден — ставлю через Homebrew: colima docker docker-compose docker-buildx"
  brew install colima docker docker-compose docker-buildx
}

# Подключает плагины compose/buildx из Homebrew и убирает хранилище паролей Docker Desktop,
# если сам Docker Desktop удалён (иначе docker pull падает на docker-credential-desktop).
fix_docker_config_macos() {
  local cfg="$HOME/.docker/config.json"
  mkdir -p "$HOME/.docker"
  [ -f "$cfg" ] || echo '{}' > "$cfg"
  have python3 || return 0
  python3 - "$cfg" "$(brew --prefix 2>/dev/null || echo /opt/homebrew)/lib/docker/cli-plugins" <<'PY'
import json, os, shutil, sys
path, plugins = sys.argv[1], sys.argv[2]
cfg = json.load(open(path))
changed = False
if os.path.isdir(plugins) and plugins not in cfg.get("cliPluginsExtraDirs", []):
    cfg.setdefault("cliPluginsExtraDirs", []).append(plugins)
    changed = True
if cfg.get("credsStore") == "desktop" and not shutil.which("docker-credential-desktop"):
    cfg.pop("credsStore")
    changed = True
if changed:
    json.dump(cfg, open(path, "w"), indent=2)
PY
}

# Запускает движок Docker на macOS, если он не отвечает: Colima или Docker Desktop.
start_docker_macos() {
  docker info >/dev/null 2>&1 && return 0
  if have colima; then
    log "Запускаю Colima (первый старт скачивает образ ВМ, ~1–3 мин)"
    colima start --cpu 4 --memory 6 --disk 60
  elif [ -d "/Applications/Docker.app" ]; then
    log "Запускаю Docker Desktop"
    open -a Docker
    for _ in $(seq 1 60); do
      docker info >/dev/null 2>&1 && return 0
      sleep 2
    done
  fi
  if ! docker info >/dev/null 2>&1; then
    echo "Docker не отвечает. Запустите Docker Desktop или выполните: colima start" >&2
    exit 1
  fi
}

if [ "$(uname -s)" = "Darwin" ]; then
  have docker || install_docker_macos
  fix_docker_config_macos
  start_docker_macos
elif ! have docker; then
  install_docker_linux
fi

if ! docker compose version >/dev/null 2>&1; then
  if [ "$(uname -s)" = "Darwin" ]; then
    echo "Не найден плагин 'docker compose'. Выполните: brew install docker-compose" >&2
  else
    echo "Не найден плагин 'docker compose'. Установите docker-compose-plugin." >&2
  fi
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
      sed -i.bak "s|^AI_TOKEN_ENCRYPTION_KEY=.*|AI_TOKEN_ENCRYPTION_KEY=${key}|" .env && rm -f .env.bak
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
