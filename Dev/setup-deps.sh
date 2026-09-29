#!/usr/bin/env bash
# setup-deps.sh — установка зависимостей для разработки и запуска тестов бэкенда.
#
# ЧТО ДЕЛАЕТ:
#   Ставит .NET SDK 10, PostgreSQL (нужен интеграционным тестам), плюс базовые
#   git/curl/tar/ca-certificates и в конце печатает версии для проверки.
#   Скрипт идемпотентный: повторный запуск ничего не ломает.
#
# ЧЕМ СТАВИТ (менеджеры пакетов):
#   * apt-get — Debian, Ubuntu, Astra Linux (DEB-дистрибутивы);
#   * dnf     — Fedora, RHEL, Red OS (RPM-дистрибутивы);
#   * brew    — macOS (git, Node.js 22, PostgreSQL 16; .NET — официальным установщиком, без sudo).
#   Базовые утилиты берутся из репозиториев дистрибутива. Если в репозитории нет нужной
#   версии .NET, автоматически используется официальный установщик (см. ниже).
#
# ОТКУДА КАЧАЕТ:
#   * apt/dnf — репозитории дистрибутива (git, curl, ca-certificates, tar, postgresql,
#     а также попытка поставить dotnet-sdk-10.0);
#   * .NET  — официальный установщик Microsoft: https://dot.net/v1/dotnet-install.sh (channel 10.0).
#
# СОВМЕСТИМОСТЬ:
#   Стабильно работает на Debian/Ubuntu, Fedora/RHEL, а также на РЕД ОС и ASTRA LINUX:
#   дистрибутив определяется по apt/dnf, а нужная версия .NET при отсутствии в репозитории
#   тянется официальным установщиком — без опоры на пакеты дистрибутива.
#
# ФРОНТЕНД:
#   Скрипт пока НЕ ставит Node.js и пакетный менеджер для фронтенда — это нужно будет добавить
#   позже (см. Skills/frontend-start).
set -euo pipefail

DOTNET_CHANNEL="${DOTNET_CHANNEL:-10.0}"

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
have() { command -v "$1" >/dev/null 2>&1; }

# macOS: Homebrew (git, Node.js 22, PostgreSQL 16) + официальный установщик .NET в $HOME/.dotnet (без sudo).
if [ "$(uname -s)" = "Darwin" ]; then
  if ! have brew; then
    echo "Нужен Homebrew: https://brew.sh — установите и повторите скрипт." >&2
    exit 1
  fi
  log "brew: git node@22 postgresql@16"
  brew install git node@22 postgresql@16
  NODE_PREFIX="$(brew --prefix node@22)"
  export PATH="$NODE_PREFIX/bin:$PATH"

  if have dotnet && dotnet --list-sdks | grep -q "^${DOTNET_CHANNEL%%.*}\."; then
    log "dotnet уже установлен: $(dotnet --version)"
  else
    log "Официальный установщик Microsoft: .NET SDK $DOTNET_CHANNEL -> \$HOME/.dotnet"
    curl -sSL https://dot.net/v1/dotnet-install.sh -o "${TMPDIR:-/tmp}/dotnet-install.sh"
    bash "${TMPDIR:-/tmp}/dotnet-install.sh" --channel "$DOTNET_CHANNEL" --install-dir "$HOME/.dotnet"
    export PATH="$HOME/.dotnet:$PATH"
  fi

  log "Готово. Версии:"
  echo "  dotnet:   $(have dotnet && dotnet --version || echo 'не найден')"
  echo "  node:     $(have node && node --version || echo 'не найден')"
  echo "  postgres: $(have psql && psql --version || echo "$(brew --prefix postgresql@16)/bin/psql")"
  echo
  echo "Добавьте в ~/.zshrc:"
  echo "  export PATH=\"\$HOME/.dotnet:$NODE_PREFIX/bin:\$(brew --prefix postgresql@16)/bin:\$PATH\""
  echo "Docker для запуска всего проекта ставит Scripts/start.sh (Colima через Homebrew)."
  exit 0
fi

SUDO=""
if [ "$(id -u)" -ne 0 ]; then
  if have sudo; then SUDO="sudo"; else echo "Нужны права root или sudo." >&2; exit 1; fi
fi

PKG=""
if have apt-get; then PKG="apt"
elif have dnf; then PKG="dnf"
else echo "Не найден ни apt-get, ни dnf — неподдерживаемый дистрибутив." >&2; exit 1; fi
log "Дистрибутив/менеджер пакетов: $PKG"

install_base() {
  case "$PKG" in
    apt)
      log "apt-get: git curl ca-certificates tar"
      $SUDO apt-get update -y
      $SUDO apt-get install -y git curl ca-certificates tar
      ;;
    dnf)
      log "dnf: git curl ca-certificates tar"
      $SUDO dnf install -y git curl ca-certificates tar
      ;;
  esac
}

install_dotnet() {
  if have dotnet; then log "dotnet уже установлен: $(dotnet --version)"; return; fi
  case "$PKG" in
    apt) log "Пробую apt-get install dotnet-sdk-${DOTNET_CHANNEL}"; $SUDO apt-get install -y "dotnet-sdk-${DOTNET_CHANNEL}" || true ;;
    dnf) log "Пробую dnf install dotnet-sdk-${DOTNET_CHANNEL}"; $SUDO dnf install -y "dotnet-sdk-${DOTNET_CHANNEL}" || true ;;
  esac
  if have dotnet; then log "dotnet установлен из репозитория: $(dotnet --version)"; return; fi

  log "Официальный установщик Microsoft: .NET SDK $DOTNET_CHANNEL -> \$HOME/.dotnet"
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel "$DOTNET_CHANNEL" --install-dir "$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
  log "Добавь в профиль: export PATH=\"\$HOME/.dotnet:\$PATH\""
}

install_postgres() {
  case "$PKG" in
    apt) log "apt-get: postgresql postgresql-client"; $SUDO apt-get install -y postgresql postgresql-client ;;
    dnf) log "dnf: postgresql-server postgresql"; $SUDO dnf install -y postgresql-server postgresql ;;
  esac
}

install_base
install_dotnet
install_postgres

log "Готово. Версии:"
echo "  dotnet:   $(have dotnet && dotnet --version || echo 'не найден')"
echo "  postgres: $(have psql && psql --version || echo 'не найден')"
echo
echo "Интеграционные тесты ждут PostgreSQL на 127.0.0.1:55432 (переопределяется через SRHC_TEST_POSTGRES)."
echo "Быстрый вариант — Docker:"
echo "  docker run -d --name srhc-test-postgres -p 55432:5432 \\"
echo "    -e POSTGRES_DB=sourcecraft_repo_health -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres postgres:16"
