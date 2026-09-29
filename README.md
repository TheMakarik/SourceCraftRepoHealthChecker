<p align="center">
  <img src="Images/favicon-96x96.png" alt="SourceCraft" width="72" height="72" />
  &nbsp;&nbsp;&nbsp;
  <img src="Images/yandex-icon.png" alt="Yandex" width="72" height="72" />
</p>

<h1 align="center">SourceCraft Repo Health Checker</h1>

<p align="center">
  Оценка «здоровья» открытых репозиториев SourceCraft: Repo Health Score 0–100, объяснение расчёта
  и приоритизированные рекомендации. Вход через <b>Я ID</b>, данные — из API и AppSec SourceCraft.
</p>

---

## О проекте

**SourceCraft Repo Health Checker** — веб-сервис, который анализирует репозитории SourceCraft и считает
итоговый балл **Repo Health Score (0–100)** по шести категориям, объясняет, из чего сложилась оценка,
и предлагает конкретные действия для улучшения. Отдельные категории со статусом **«Нет данных»**
не штрафуют проект, а показываются как есть.

Источники данных — только действующие интерфейсы SourceCraft:
- **REST API SourceCraft** — каталог, репозитории, issues, merge requests, релизы, CI/CD;
- **git-история репозитория** — коммиты, контрибьюторы, документация, структура;
- **AppSec SourceCraft** — реальные результаты SAST/SCA/secret scanning (без имитации сканирования).

---

## Возможности

**Рейтинг и анализ**
- Публичный рейтинг открытых репозиториев: место, проект, ссылка, Score, лайки, язык, последняя активность.
- Фильтр по языкам (только реально присутствующие), **мультивыбор** (Ctrl/Cmd + клик или долгое нажатие),
  сортировки по Score / лайкам / активности, постраничная навигация, переход в SourceCraft и на аккаунт владельца.
- Страница анализа: итоговый Score, оценки по 6 категориям, метрики с объяснением, сильные/слабые стороны,
  детальные рекомендации (проблема, почему важно, факты, действие, приоритет, ожидаемое влияние на Score, ссылка на источник).
- **Отчёты**: Markdown, JSON, HTML и PDF.

**Авторизация и приватность**
- Вход через **Я ID** (identity) + **PAT SourceCraft** (доступ к данным) — PAT хранится зашифрованно.
- Публичные страницы доступны без входа; приватные данные — только владельцу (единый guard доступа).
- Сервисный токен изолирован от пользовательских запросов.

**Работа с кодом и аналитика**
- Просмотр **файлов и папок** в виде TreeView с подсветкой синтаксиса.
- **Анализ папок** по критериям (файлы, TODO/FIXME, README/LICENSE/tests).
- **Сравнение** 2–4 репозиториев по Score, категориям и метрикам.
- **Индекс достоверности** (защита публичного рейтинга от накрутки), флаг в рейтинге.
- **Bus factor** и карта владельцев (по git-истории).
- История Score и графики динамики.

**AI (сверх ТЗ)**
- AI-резюме и разборы: рекомендации, объяснение расчёта, план улучшения, триаж безопасности, прогноз рисков.
- **Потоковый вывод (SSE)** с отображением «размышлений» reasoning-моделей.
- Провайдеры: OpenAI, Anthropic, Google Gemini, Yandex, DeepSeek. **Токен — отдельный на каждый провайдер**,
  хранится зашифрованно; есть «Тестовый запрос».

**Эксплуатация**
- Регулярный пересчёт по расписанию (планировщик + очередь анализа + распределённая аренда).
- Устойчивость к недоступности источников (статусы «Нет данных»/«Недоступно», ретраи).
- Readiness-проба `/healthz` с проверкой БД.

---

## Стек

**Backend (C#, единый сервис):**
- **.NET 10** / ASP.NET Core (minimal API)
- **EF Core + PostgreSQL**
- **Refit** — клиенты SourceCraft REST и AppSec
- **LibGit2Sharp** — git-аналитика
- **Minio** — S3-совместимое хранилище (ключи DataProtection)
- **Microsoft.Extensions.AI** — AI-провайдеры (OpenAI-совместимые)
- **Serilog**, **DotNetEnv**

**Frontend:**
- **React 18 + TypeScript + Vite**
- **Fluent UI v9**, **TanStack Query**, **React Router**, **Recharts**
- **react-markdown** (рендер ответов ИИ), **zod + react-hook-form**

**Инфраструктура:**
- **Docker Compose** (PostgreSQL + backend + frontend), **nginx** (SPA + прокси API/WS)

---

## Архитектура

Луковичная (onion) архитектура, зависимости — строго внутрь, к `Domain`:

| Проект | Ответственность | Зависит от |
|---|---|---|
| `Backend/SourceCraftRepoHealthChecker.Domain` | Сущности, value-объекты, доменные правила | — |
| `Backend/SourceCraftRepoHealthChecker.Application` | Сценарии использования, порты (абстракции) | `Domain` |
| `Backend/SourceCraftRepoHealthChecker.infrastructure` | EF Core, внешние API (Refit), git (LibGit2Sharp), S3 (Minio), DotEnv | `Application`, `Domain` |
| `Backend/SourceCraftRepoHealthChecker.Presenter` | Веб-API, эндпоинты, DI (composition root) | `Application`, `infrastructure` |

Фронтенд — SPA (`Frontend/`), общается с API как с единственным origin (через nginx-прокси в Docker).

Подробнее: [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md), контракт API — [`Docs/API.md`](Docs/API.md).

---

## Сборка и установка

Репозиторий можно скачать с **любого из двух сайтов** (на выбор):

- **GitHub:** https://github.com/TheMakarik/SourceCraftRepoHealthChecker
- **SourceCraft:** https://sourcecraft.dev/themakarik/repo-health-checker

> Важно: стартовые скрипты рассчитаны на запуск из репозитория (им нужны `docker-compose.yml`,
> `Backend/`, `Frontend/`). Скачивать только файл скрипта недостаточно — скачиваем репозиторий, затем запускаем скрипт.

### Требования
- **Docker** (скрипт `start.sh` / `start.bat` сам поставит его, если не найден).
- Для локальной разработки без Docker: **.NET SDK 10**, **Node.js 22+** (скрипт `Dev/setup-deps.*`).

### Быстрый старт (интерактивно — рекомендуется)

```bash
# 1. Скачать репозиторий (любой из двух)
git clone https://github.com/TheMakarik/SourceCraftRepoHealthChecker.git          # GitHub
git clone https://git.sourcecraft.dev/themakarik/repo-health-checker.git          # SourceCraft

cd SourceCraftRepoHealthChecker   # или repo-health-checker

# 2. Интерактивная подготовка и запуск
bash Scripts/prepare.sh     # Linux/macOS
Scripts\prepare.bat         # Windows
```

`Scripts/prepare.sh` / `Scripts/prepare.bat` спросят нужные токены, **объяснят, где их взять** (Я ID ClientID/Secret,
опционально сервисный SourceCraft PAT), сгенерируют ключ шифрования, создадут `.env` и запустят проект.

### Вариант 1 — Docker (вручную)

```bash
# 1. Скачать репозиторий (любой из двух)
git clone https://github.com/TheMakarik/SourceCraftRepoHealthChecker.git          # GitHub
git clone https://git.sourcecraft.dev/themakarik/repo-health-checker.git          # SourceCraft

cd SourceCraftRepoHealthChecker   # или repo-health-checker

# 2. Подготовить секреты
cp .env.example .env
#    заполнить: YANDEX_CLIENT_ID / YANDEX_CLIENT_SECRET (приложение Я ID),
#    опционально SOURCECRAFT_PAT (сервисный токен для публичного рейтинга)

# 3. Запустить (поставит Docker при отсутствии и поднимет Postgres + backend + frontend)
bash Scripts/start.sh          # Linux/macOS
Scripts\start.bat              # Windows
```

Открыть: **http://localhost:8080** (API — http://localhost:5172).

> Для входа через Я ID укажите в приложении Я ID Redirect URI: `http://localhost:8080/auth/callback`.

### Вариант 2 — локальная разработка

```bash
bash Dev/setup-deps.sh         # .NET SDK 10 (Node.js для фронтенда ставится отдельно)
# Backend
#   задать в .env/переменных: ConnectionStrings, AiTokenEncryptionOptions:Key,
#   DataProtectionStorageOptions:KeyPath, YandexIdOptions, SourceCraftServiceOptions:ServiceToken
dotnet run --project Backend/SourceCraftRepoHealthChecker.Presenter   # :5172

# Frontend
cd Frontend && npm install && npm run dev                             # :5173 (проксирует /api,/auth)
```

### Тесты

```bash
SRHC_TEST_POSTGRES="Host=127.0.0.1;Port=55432;Database=sourcecraft_repo_health_tests;Username=postgres;Password=postgres" \
  dotnet test SourceCraftRepoHealthChecker.slnx
```

Интеграционные тесты требуют PostgreSQL (в CI поднимается сервисом `postgres:16`).

---

## Конфигурация

Секреты и настройки берутся из `.env` (в корне, не коммитится) и переменных окружения:

| Переменная | Назначение |
|---|---|
| `POSTGRES_PASSWORD` | пароль PostgreSQL для docker-compose |
| `AI_TOKEN_ENCRYPTION_KEY` | base64‑ключ 32 байта для шифрования токенов (обязателен) |
| `SOURCECRAFT_PAT` | сервисный токен для публичного рейтинга (опционально) |
| `YANDEX_CLIENT_ID` / `YANDEX_CLIENT_SECRET` | OAuth‑приложение Я ID |
| `YANDEX_REDIRECT_URI` | callback, напр. `http://localhost:8080/auth/callback` |

Настройки методики и AI — в `Backend/SourceCraftRepoHealthChecker.Presenter/appsettings.json`.

---

## Документация

- [`Docs/API.md`](Docs/API.md) — публичные эндпоинты и источники данных.
- [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) — архитектура, методика Score, метрики, ограничения, масштабирование.
- [`Docs/DELIVERY.md`](Docs/DELIVERY.md) — чек-лист финальной сдачи проекта.

---

## Лицензия

См. [`LICENSE.txt`](LICENSE.txt).
