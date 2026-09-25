# SourceCraftRepoHealthChecker

Веб-сервис оценки «здоровья» открытых репозиториев [SourceCraft](https://sourcecraft.tech). Считает **Repo Health Score** (0–100) по шести категориям, объясняет оценку и формирует приоритизированные рекомендации. Вход — через **Я ID**.

Единственный источник данных — действующие **API и CLI SourceCraft**. Импорт с внешних Git-платформ не выполняется, security-оценка строится только на реальных результатах **AppSec SourceCraft** (SAST, SCA, secret scanning) — без имитации сканирования.

## Категории Repo Health Score

| Категория | Вес |
|---|---|
| Security | 20% |
| Code health | 20% |
| Активность | 15% |
| Документация и лучшие практики | 15% |
| CI/CD | 15% |
| Issues | 15% |

Отсутствие данных — отдельный статус **«Нет данных»** (`NoData`), а не плохой результат. Категории со статусом «Нет данных» исключаются из расчёта, а веса доступных нормируются. Подробности — в [docs/methodology.md](docs/methodology.md).

## Архитектура

Бэкенд разделён на два сервиса:

- **C# (луковичная архитектура)** — методика Score, агрегация, рекомендации, БД (PostgreSQL), веб-API и HTML-страницы (composition root). Проекты: `Domain` → `Application` → `infrastructure` → `Presenter`.
- **Go** — ядро доступа к SourceCraft: сбор фактов через API/CLI, клон и анализ git-истории, нормализация в контракт, краткоживущие S3-снапшоты с TTL, serverless-развёртывание (Cloud Functions, Serverless Containers, Message Queue, Timer). Go не знает про веса и категории — только отдаёт факты.

Диаграмма зависимостей и детали — в [docs/architecture.md](docs/architecture.md).

## Быстрый старт

Требования: **.NET SDK 10**, **Go 1.26+**, **PostgreSQL**, **Docker** (для локального S3), установленный **git** (Go-сервис использует локальный git).

### 1. Go-сервис (сбор данных)

```bash
cd Backend/Go
cp .env.example .env          # заполнить SOURCECRAFT_TOKEN
docker compose up --build     # сервис :8080, MinIO :9000 (консоль :9001)
```

Без Docker: поднять любое S3-совместимое хранилище, выставить переменные из `.env.example` и запустить:

```bash
cd Backend/Go
go run ./cmd/server            # HTTP-сервер, :8080 (HTTP_ADDR)
go run ./cmd/function          # serverless-точка входа (Functions + триггеры)
go test -race ./...            # тесты (нужен git)
```

Ключевые переменные Go-сервиса: `SOURCECRAFT_TOKEN` (PAT сервиса), `S3_ENDPOINT`/`S3_BUCKET`/`S3_ACCESS_KEY`/`S3_SECRET_KEY` (обязательны), `YANDEX_CLIENT_ID`/`YANDEX_CLIENT_SECRET`/`YANDEX_REDIRECT_URI` (Я ID; без них `/auth/url` и `/auth/token` → `501`), `INTERNAL_TOKEN` (защита служебных эндпоинтов), `SNAPSHOT_TTL` (по умолчанию `3h`). Полный список — в `Backend/Go/.env.example` и [docs/api.md](docs/api.md).

### 2. C#-бэкенд (API, страницы, методика)

```bash
dotnet build SourceCraftRepoHealthChecker.slnx
dotnet test  SourceCraftRepoHealthChecker.slnx

# запуск веб-сервиса (профиль http, http://localhost:5172)
dotnet run --project Backend/CSharp/SourceCraftRepoHealthChecker.Presenter
```

Настройки — в `Backend/CSharp/SourceCraftRepoHealthChecker.Presenter/appsettings.json`. Значения, которые нужно задать, удобно передавать переменными окружения (двойное подчёркивание разделяет уровни) или через user-secrets:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Database=sourcecraft_repo_health;Username=postgres;Password=postgres"
export SourceCraftServiceOptions__BaseUrl="http://localhost:8080"
export AiTokenEncryptionOptions__Key="<ключ шифрования токенов ИИ>"
```

`dotnet user-secrets` требует инициализации в проекте: `dotnet user-secrets init --project Backend/CSharp/SourceCraftRepoHealthChecker.Presenter`.

PostgreSQL подключается через EF Core/Npgsql (`DefaultConnection`). Схема строится по модели EF Core; в репозитории миграции пока не закоммичены — интеграционные тесты создают схему через `EnsureCreated`.

## Реализованные пользовательские сценарии

- Публичный рейтинг: `GET /rating` — место, проект, ссылка, Score, лайки, язык, последняя активность; фильтр по языку; сортировка по Score, лайкам и активности; переход на страницу анализа.
- Страница анализа: `GET /repositories/{id}` — итоговый Score, оценки по категориям, сильные и слабые стороны, рекомендации, «Нет данных», дата последнего анализа.
- Выгрузка отчёта: `GET /api/repositories/{id}/report.md` (Markdown).
- JSON-API: `GET /api/repositories`, `GET /api/repositories/{id}/analysis`, `POST /api/repositories/refresh`.

Актуальный список HTTP-эндпоинтов и внутренний контракт C#↔Go — в [docs/api.md](docs/api.md).

### Статус отдельных сценариев

- **Авторизация через Я ID.** На стороне Go контракт входа (`POST /auth/url`, `POST /auth/token`, `GET /auth/me`, `GET /auth/repositories`) и обмен кода реализованы; в C# есть адаптер `SourceCraftAuthenticationAdapter` и use case'ы `IAuthenticateUserUseCase`/`IGetUserRepositoriesUseCase` (создание/обновление `User`). HTTP-маршруты входа и прокидка PAT пользователя в `Presenter` на момент написания не подключены (детали и текущий статус — в [docs/api.md](docs/api.md)).
- **Периодический пересчёт.** В `Application`/`infrastructure` реализованы `ScheduledAnalysisRunner` и `ScheduledAnalysisBackgroundService` (обновление каталога + анализ устаревших репозиториев по таймеру). Регистрация фонового сервиса и секция `SchedulingOptions` в конфигурации на момент написания не подключены. Дополнительно на стороне Go есть таймер-триггер очистки снапшотов и триггер очереди.

## Часть со звёздочкой (опционально, не реализовано)

Расширенный рейтинг, история и сравнение проектов, публичный API и quality badges, AI-summary/AI-рекомендации, защита публичного рейтинга от накрутки. Под часть ИИ в домене есть сущности (`UserAi`, `AiProviders`) и шифрование токена (`IAiTokenProtector`), но пользовательских сценариев и HTTP-эндпоинтов пока нет. Бонусные функции не компенсируют обязательную часть.

## Структура репозитория

```
.
├── Backend/
│   ├── CSharp/
│   │   ├── SourceCraftRepoHealthChecker.Domain/           # Ядро: сущности, enum'ы, доменные правила
│   │   ├── SourceCraftRepoHealthChecker.Application/      # Сценарии, абстракции (порты), методика Score
│   │   ├── SourceCraftRepoHealthChecker.infrastructure/   # EF Core/Npgsql, HTTP-адаптеры SourceCraft, Scrutor
│   │   └── SourceCraftRepoHealthChecker.Presenter/        # Точка входа: minimal API, HTML, DI
│   └── Go/                                                # Go-сервис сбора данных (API/CLI, git, S3, serverless)
├── Tests/
│   ├── SourceCraftRepoHealthChecker.UnitTests/            # xUnit + FakeItEasy + FluentAssertions + AutoFixture
│   └── SourceCraftRepoHealthChecker.IntegrationTests/     # EF Core (SQLite), сценарии методики, git
├── Skills/                                                # Скиллы opencode (csharp-*)
├── docs/                                                  # Документация (этот каталог)
├── AGENTS.md
├── LICENSE.txt
├── opencode.json
└── SourceCraftRepoHealthChecker.slnx
```

## Документация

- [docs/architecture.md](docs/architecture.md) — слои C#, роль Go, данные, S3-снапшоты, serverless, DI.
- [docs/methodology.md](docs/methodology.md) — формула Score, веса, метрики категорий, «Нет данных», рекомендации.
- [docs/api.md](docs/api.md) — HTTP-эндпоинты Presenter и контракт C#↔Go.
- [docs/data-sources.md](docs/data-sources.md) — API/CLI SourceCraft, ограничения, правила хранения кода.
- [docs/limitations-and-scaling.md](docs/limitations-and-scaling.md) — ограничения, крупные репозитории, устойчивость, масштабирование.
- [Backend/Go/README.md](Backend/Go/README.md) — эксплуатация Go-сервиса.

## Ограничения источников (кратко)

- **AppSec** (SAST/SCA/secret scanning) в публичном API SourceCraft отсутствует → Security-категория получает статус «Нет данных» без штрафа и пометку «Подключите AppSec SourceCraft».
- **Я ID не даёт доступа к API SourceCraft** (API принимает только PAT/IAM) → приватные репозитории требуют PAT пользователя.
- Исходный код не хранится дольше, чем нужно для анализа: только нормализованные метрики персистентны, клон — краткоживущий S3-снапшот с TTL.

Полный разбор — в [docs/data-sources.md](docs/data-sources.md).
