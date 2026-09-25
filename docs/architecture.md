# Архитектура

## Общая схема

Два бэкенда с чёткими зонами ответственности:

```
Клиент (браузер)
   │
   ▼
C# Presenter (ASP.NET Core minimal API + HTML)
   │  порты Application: ISourceCraft* / IGitRepositoryReader
   ▼
Go-сервис SourceCraft (HTTP, REST/JSON)
   │
   ├── публичный REST API SourceCraft  (каталог, issues, MR, CI, releases)
   ├── локальный git                   (clone, log, TODO/FIXME, состав файлов)
   ├── Я ID OAuth                      (вход, только личность)
   └── S3-совместимое хранилище        (краткоживущие снапшоты репозитория)
```

- **C#** владеет методикой Repo Health Score, агрегацией, рекомендациями, БД, веб-API и HTML-страницами.
- **Go** владеет доступом к SourceCraft, клонированием/анализом git и временным хранением рабочей копии. Go **не знает** про веса, категории, нормализацию и рекомендации — только отдаёт факты ([Backend/Go/TZ.md](../Backend/Go/TZ.md), раздел 1 и 11).

## Слои C# (луковичная архитектура)

Зависимости направлены строго внутрь, к `Domain`.

| Проект | Ответственность | Зависит от |
|---|---|---|
| `SourceCraftRepoHealthChecker.Domain` | Ядро: сущности (`Repository`, `AnalysisRun`, `CategoryScore`, `Recommendation`, `User`, `UserAi`), enum'ы (`ScoreCategory`, `DataStatus`, `AnalysisStatus`, `RecommendationPriority`, `AiProviders`). Не зависит ни от чего. | — |
| `SourceCraftRepoHealthChecker.Application` | Сценарии использования и абстракции (порты). Методика Score (`HealthCheck/`), рейтинг (`Rating/`), авторизация (`Authentication/`), планировщик (`Scheduling/`), порты SourceCraft (`SourceCraft/Interfaces/`), интерфейс БД (`Persistence/Interfaces/`), опции, Markdown-рендер отчёта. | `Domain` |
| `SourceCraftRepoHealthChecker.infrastructure` | Адаптеры: `RepoHealthCheckerDbContext` (EF Core/Npgsql), HTTP-клиент и адаптеры SourceCraft, `LocalGitRepositoryReader` (LibGit2Sharp), `SourceCraftAccessTokenAccessor`, фоновый планировщик, шифрование токена ИИ. | `Application`, `Domain` |
| `SourceCraftRepoHealthChecker.Presenter` | Точка входа: ASP.NET Core minimal API, HTML-рендеры, Serilog, DI (composition root). | `Application`, `infrastructure` |

Правила направлений:

- `Domain` не знает о БД, фреймворках и внешних сервисах.
- `Application` зависит только от `Domain` и собственных абстракций; не ссылается на `infrastructure`.
- `infrastructure` реализует порты `Application`; обратные зависимости недопустимы.
- `Presenter` — единственный, кто ссылается и на `Application`, и на `infrastructure`, чтобы собрать DI-граф.

## Роль Go-сервиса

Go-сервис — это ядро сбора и нормализации фактов, спрятанное за портами `Application/SourceCraft/Interfaces/`:

| Порт C# | Что отдаёт Go |
|---|---|
| `ISourceCraftAuthentication` | вход через Я ID, текущий пользователь, доступные пользователю репозитории |
| `ISourceCraftRepositoryCatalog` | публичный каталог и карточка репозитория |
| `ISourceCraftActivitySource` | коммиты, contributors, релизы |
| `ISourceCraftCollaborationSource` | issues, merge requests |
| `ISourceCraftPipelineSource` | запуски CI/CD |
| `ISourceCraftCodeHealthSource` | TODO/FIXME и давность самого старого маркера |
| `ISourceCraftDocumentationSource` | наличие README/LICENSE/CONTRIBUTING/CODEOWNERS и инструкций |
| `ISourceCraftSecuritySource` | результаты AppSec (сейчас всегда `Unavailable`, см. [data-sources.md](data-sources.md)) |

C# общается с Go по HTTP (`SourceCraftHttpClient`), база — `SourceCraftServiceOptions.BaseUrl` (по умолчанию `http://localhost:8080`). Каждый ответ разбирается в `SourceCraftResult<T>` со статусом `Available`/`NoData`/`Unavailable`. Контракт и эндпоинты — в [api.md](api.md).

`IGitRepositoryReader` — отдельный порт для локального анализа git; в инфраструктуре реализован `LocalGitRepositoryReader` на LibGit2Sharp. Основной путь анализа репозитория в проде — через Go (git-история на стороне сервиса); локальный ридер используется в тестовых сценариях и создаётся вручную (`new LocalGitRepositoryReader()`), потому что Scrutor регистрирует только классы с суффиксом `Adapter`, а не `Reader`.

Пакеты Go:

| Пакет | Назначение |
|---|---|
| `cmd/server` | точка входа обычного HTTP-сервера (локально и в Serverless Container) |
| `cmd/function` | точка входа Cloud Functions (тот же набор маршрутов + триггеры) |
| `internal/serverless` | сборка serverless-обработчика (API + очередь + таймер) |
| `internal/queue` | потребитель триггера Message Queue (создание снапшотов) |
| `internal/sourcecraft` | клиент API: ретраи/backoff, `Retry-After`, rate limit, пагинация, circuit breaker |
| `internal/gitrepo` | bare partial clone, потоковый `git log`; креды только через env процесса git |
| `internal/objectstore` | интерфейс S3 и реализация на `minio-go` |
| `internal/snapshot` | жизненный цикл снапшота в S3 |
| `internal/service` | маппинг фактов SourceCraft в модели C# |
| `internal/httpapi` | REST-эндпоинты, конверт ответа, маппинг ошибок |
| `internal/contract` | JSON-контракт с C# |
| `internal/yandexid` | OAuth Я ID |
| `internal/config` | загрузка настроек из переменных окружения |

## Данные через API + CLI/локальный git SourceCraft

Единственный источник данных — API и CLI SourceCraft, поэтому часть фактов доступна только из git-истории, а не из REST:

- **Каталог, карточка репозитория, issues, MR, CI-запуски, релизы** — публичный REST API SourceCraft (`https://api.sourcecraft.tech`).
- **Коммиты и contributors** — из git-истории (в API их нет): bare partial clone, потоковый `git log`; авторы объединяются по email, логин — имя автора из git.
- **TODO/FIXME и давность комментариев** — по git-истории с содержимым файлов (`withBlobs`).
- **Документация** — по составу файлов и содержимому README/CONTRIBUTING.

Git-креды передаются только через `http.extraHeader` и `GIT_CONFIG_*` переменные окружения процесса git — токена нет ни в remote URL, ни в argv, ни в логах.

## S3-снапшоты с TTL

Клон репозитория нужен для анализа, поэтому рабочая копия хранится **временно** в S3-совместимом объектном хранилище (Yandex Object Storage, MinIO, AWS). Исходный код — не персистентные данные.

- **Формат**: `git clone --bare --single-branch --no-tags --filter=blob:none`. Хранится история и деревья, содержимого файлов нет. `withBlobs: true` — полный клон ветки, `depth` — shallow.
- **Ключ**: `{S3_PREFIX}/{repositoryId}/{runId}/repo.tar.gz`; параллельные прогоны изолированы.
- **Загрузка**: tar.gz упаковывается потоком в multipart upload частями по 16 МБ, без промежуточного файла; локальный клон удаляется сразу.
- **Шифрование**: SSE-S3 (`S3_SSE=true`).
- **TTL**: метаданные `expires-at` (`SNAPSHOT_TTL`, по умолчанию 3 часа) — просроченный снапшот не читается.
- **Удаление** гарантируют четыре механизма: явный `DELETE` после анализа; проверка `expires-at`; reaper `POST /internal/snapshots/reap` (таймер-триггер или `SNAPSHOT_REAP_INTERVAL` локально); lifecycle-правило бакета (`S3_LIFECYCLE_DAYS`, по умолчанию 1 день) и очистка незавершённых multipart-загрузок.
- **Доступ**: перед каждой операцией со снапшотом сервис проверяет через API, что токен запроса видит репозиторий; чужой снапшот по известному `runId` не прочитать.
- **Без `runId`**: activity-эндпоинты делают эфемерный клон только на время запроса; `code-health`/`documentation` клонируют с блобами.
- **Лимиты**: `GIT_MAX_REPO_MB` (размер клона и распаковки) и `GIT_CLONE_TIMEOUT`; при превышении — `Unavailable`.

Локально S3 заменяется MinIO (`docker-compose.yml`).

## Serverless (Yandex Cloud)

Развёртывание Go-части частично serverless, разделение по характеру нагрузки:

| Тип | Что там | Почему |
|---|---|---|
| Cloud Functions | auth, каталог, issues/MR, AppSec, CI-статусы | короткие HTTP-вызовы, укладываются в лимиты FaaS |
| Serverless Containers | клон и анализ крупных репозиториев, git-история, TODO/FIXME | нет лимита времени FaaS, нужны диск и память под clone/stream |
| Message Queue | очередь задач анализа, fan-out по репозиториям | устойчивость, ретраи, разгрузка |
| Timer trigger | периодический пересчёт | регулярный пересчёт по расписанию |
| Object Storage (S3) | краткоживущий буфер репозитория | TTL + шифрование, удаление после анализа |

Требования serverless выполнены на стороне Go: stateless (состояние не в памяти инстанса, токены/курсоры — в запросе), идемпотентность (повторный `CreateSnapshot` по `(repositoryId, runId)` не клонирует заново), локальный запуск тем же кодом (`cmd/server`). Тяжёлое не идёт в Function. В serverless-точке входа включается circuit breaker к SourceCraft.

## DI (MS Host + Scrutor)

`Presenter/Program.cs` использует generic host ASP.NET Core (`WebApplication.CreateBuilder`). Регистрация:

- `AddApplication(configuration)` — опции `HealthCheckOptions`, сервисы методики (`Singleton`) и use case'ы (`Scoped`).
- `AddInfrastructure(configuration)` — опции (`SourceCraftServiceOptions`, `UserOptions`, …), `DbContext` на Npgsql, `SourceCraftHttpClient` через `AddHttpClient`, `IAiTokenProtector`.
- **Scrutor**: `infrastructure` сканирует собственную сборку и регистрирует все классы с суффиксом `Adapter` по реализуемым интерфейсам со `Scoped`-временем жизни. Так подключаются все `SourceCraft*Adapter` без ручного перечисления.
- `TimeProvider.System` регистрируется как singleton для воспроизводимого расчёта времени.

## Фоновый анализ и токен доступа

- **Планировщик.** `Application/Scheduling` содержит `IScheduledAnalysisRunner`/`ScheduledAnalysisRunner` (обновляет каталог, выбирает репозитории с `AnalyzedAt == null` или старше `AnalysisIntervalMinutes`, анализирует не более `MaxRepositoriesPerRun` за прогон, возвращает `ScheduledAnalysisSummary`) и `SchedulingOptions` (`Enabled`, `RepositoriesRefreshMinutes`, `AnalysisIntervalMinutes`, `MaxRepositoriesPerRun`). `infrastructure/Scheduling/ScheduledAnalysisBackgroundService` — `BackgroundService` на `PeriodicTimer`, запускающий раннер в отдельном DI-scope. Регистрация сервиса и секция опций на момент написания ещё не подключены в `Presenter`.
- **Токен доступа.** `ISourceCraftAccessTokenAccessor` (`Application`) с реализацией `SourceCraftAccessTokenAccessor` (`infrastructure`) хранит PAT текущего пользователя в `AsyncLocal`. `SourceCraftHttpClient` использует его как запасной вариант, когда явный токен не передан, чтобы приватные данные читались в рамках прав пользователя.

## Хранение и БД

- Персистентно сохраняются только **нормализованные метрики**: `Repository`, `AnalysisRun`, `CategoryScore`, `Recommendation` (+ `User`, `UserAi`). Сам код репозитория в БД не попадает.
- `Presentation → DbContext` идёт только через интерфейс `IRepoHealthCheckerDbContext` из `Application`; реализация — `RepoHealthCheckerDbContext` в `infrastructure`.
- Дата/время хранятся как `DateTimeOffsetToBinaryConverter`; длины строк берутся из опций (`UserOptions`, `RepositoryOptions`, `RecommendationOptions`), без хардкода.
