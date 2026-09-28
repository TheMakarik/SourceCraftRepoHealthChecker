#!/usr/bin/env bash
# setup-deps.sh — установка зависимостей для разработки и запуска тестов бэкенда.
#
# ЧТО ДЕЛАЕТ:
#   Ставит .NET SDK 10 (плюс базовые git/curl/tar/ca-certificates) и в конце печатает
#   версии для проверки. Скрипт идемпотентный: повторный запуск ничего не ломает.
#
# ЧЕМ СТАВИТ (менеджеры пакетов):
#   * apt-get — Debian, Ubuntu, Astra Linux (DEB-дистрибутивы);
#   * dnf     — Fedora, RHEL, Red OS (RPM-дистрибутивы).
#   Базовые утилиты берутся из репозиториев дистрибутива. Если в репозитории нет нужной
#   версии .NET, автоматически используется официальный установщик (см. ниже).
#
# ОТКУДА КАЧАЕТ:
#   * apt/dnf — репозитории дистрибутива (git, curl, ca-certificates, tar, а также попытка
#     поставить dotnet-sdk-10.0);
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

install_base
install_dotnet

log "Готово. Версии:"
echo "  dotnet: $(have dotnet && dotnet --version || echo 'не найден')"
