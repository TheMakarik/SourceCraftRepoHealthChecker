#!/usr/bin/env bash
# setup-deps.sh — установка зависимостей для разработки и запуска тестов бэкенда.
#
# ЧТО ДЕЛАЕТ:
#   Ставит .NET SDK 10 и Go 1.26 (плюс базовые git/curl/tar/ca-certificates) и в конце
#   печатает версии для проверки. Скрипт идемпотентный: повторный запуск ничего не ломает.
#
# ЧЕМ СТАВИТ (менеджеры пакетов):
#   * apt-get — Debian, Ubuntu, Astra Linux (DEB-дистрибутивы);
#   * dnf     — Fedora, RHEL, Red OS (RPM-дистрибутивы).
#   Базовые утилиты берутся из репозиториев дистрибутива. Если в репозитории нет нужной
#   версии .NET/Go, автоматически используется официальный установщик (см. ниже).
#
# ОТКУДА КАЧАЕТ:
#   * apt/dnf — репозитории дистрибутива (git, curl, ca-certificates, tar, а также попытка
#     поставить dotnet-sdk-10.0 / golang);
#   * .NET  — официальный установщик Microsoft: https://dot.net/v1/dotnet-install.sh (channel 10.0);
#   * Go    — официальный архив: https://go.dev/dl/go<версия>.linux-amd64.tar.gz (в /usr/local/go).
#
# СОВМЕСТИМОСТЬ:
#   Стабильно работает на Debian/Ubuntu, Fedora/RHEL, а также на РЕД ОС и ASTRA LINUX:
#   дистрибутив определяется по apt/dnf, а нужные версии .NET и Go при отсутствии в репозитории
#   тянутся официальными установщиками — без опоры на пакеты дистрибутива.
#
# ФРОНТЕНД:
#   Скрипт пока НЕ ставит Node.js и пакетный менеджер для фронтенда — это нужно будет добавить
#   позже (см. Skills/frontend-start).
set -euo pipefail

DOTNET_CHANNEL="${DOTNET_CHANNEL:-10.0}"
GO_VERSION="${GO_VERSION:-1.26.2}"
GO_INSTALL_DIR="${GO_INSTALL_DIR:-/usr/local}"

log() { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
have() { command -v "$1" >/dev/null 2>&1; }

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

install_go() {
  if have go && go version | grep -q "go${GO_VERSION%%.[0-9]*}"; then
    log "go уже установлен: $(go version)"; return
  fi
  case "$PKG" in
    apt) log "Пробую apt-get install golang-go"; $SUDO apt-get install -y golang-go || true ;;
    dnf) log "Пробую dnf install golang"; $SUDO dnf install -y golang || true ;;
  esac
  if have go; then log "go установлен из репозитория: $(go version)"; return; fi

  log "Официальный архив go.dev: go$GO_VERSION -> $GO_INSTALL_DIR/go"
  local archive="go${GO_VERSION}.linux-amd64.tar.gz"
  curl -sSL "https://go.dev/dl/${archive}" -o "/tmp/${archive}"
  $SUDO rm -rf "$GO_INSTALL_DIR/go"
  $SUDO tar -C "$GO_INSTALL_DIR" -xzf "/tmp/${archive}"
  export PATH="$GO_INSTALL_DIR/go/bin:$PATH"
  log "Добавь в профиль: export PATH=\"$GO_INSTALL_DIR/go/bin:\$PATH\""
}

install_base
install_dotnet
install_go

log "Готово. Версии:"
echo "  dotnet: $(have dotnet && dotnet --version || echo 'не найден')"
echo "  go:     $(have go && go version || echo 'не найден')"
