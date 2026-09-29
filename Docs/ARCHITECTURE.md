# Архитектура и методика SourceCraftRepoHealthChecker

Документ описывает реальную архитектуру сервиса, источники данных, формулу Repo Health Score,
состав метрик, работу с отсутствующими данными, алгоритм рекомендаций, ограничения,
подход к масштабированию, реализованные функции со звёздочкой и использование внешних
сервисов и ИИ. Контракт API — в [`API.md`](API.md).

## 1. Архитектура

Проект — единый веб-сервис с **луковичной архитектурой** на C# и SPA-фронтендом.

| Проект | Ответственность | Зависит от |
|--------|-----------------|------------|
| `Backend/SourceCraftRepoHealthChecker.Domain` | Ядро: сущности (`Entities/`), перечисления (`Enums/`), доменные правила. Не зависит ни от чего. | — |
| `Backend/SourceCraftRepoHealthChecker.Application` | Сценарии использования и абстракции (порты): `HealthCheck`, `Rating`, `Scheduling`, `Ai`, `Ownership`, `Integrity`. | `Domain` |
| `Backend/SourceCraftRepoHealthChecker.infrastructure` | Адаптеры: EF Core/PostgreSQL, внешние HTTP (Refit), git (LibGit2Sharp), S3 (Minio), шифрование, фоновые сервисы. | `Application`, `Domain` |
| `Backend/SourceCraftRepoHealthChecker.Presenter` | Точка входа: веб-API, HTML-страницы, WebSocket, DI (composition root), `appsettings.json`. | `Application`, `infrastructure` |

Правила зависимостей:

- `Domain` не знает о БД, фреймворках и внешних сервисах.
- `Application` зависит только от `Domain` и собственных абстракций.
- `infrastructure` реализует порты `Application`; обратные зависимости недопустимы.
- `Presenter` — единственный composition root, наружу зависимостей нет.

Фронтенд (`Frontend/`) — SPA на **React + TypeScript + Fluent UI + Vite**; запросы к API через
`fetch` и TanStack Query, графики на Recharts, статус анализа — по WebSocket `/ws/analysis`.
Общий контракт описан в `Frontend/src/shared/api/types.ts`.

Сбор данных, git-аналитика и работа с хранилищем выполняются **внутри сервиса**; отдельного
Go-микросервиса нет. Конфигурация читается из `appsettings.json`, переменных окружения и `.env`
(через порт `IEnvironmentFileLoader`, реализованный `DotEnvEnvironmentFileLoader`).

## 2. Инструкция сборки и запуска

### Зависимости
- .NET SDK **10** (бэкенд), Node.js + npm (фронтенд), PostgreSQL; Docker — опционально.
- Скрипт `Dev/setup-deps.sh` (или `Dev/setup-deps.bat`) ставит .NET SDK и базовые утилиты.

### Бэкенд
```bash
dotnet build SourceCraftRepoHealthChecker.slnx -c Release
dotnet test  SourceCraftRepoHealthChecker.slnx -c Release
```
Те же команды в скриптах: `Scripts/build.ps1`, `Scripts/test.ps1`.

### Фронтенд
```bash
cd Frontend
npm install
npm run dev      # разработка
npm run build    # production-сборка (tsc -b && vite build)
```

### Запуск через Docker Compose
```bash
cp .env.example .env      # заполнить AI_TOKEN_ENCRYPTION_KEY, SOURCECRAFT_PAT, ключи Я ID
docker compose up --build -d
# либо Scripts/start.sh (Linux) / Scripts/start.bat (Windows)
```
`docker compose` поднимает **PostgreSQL + backend + frontend** (MinIO в compose нет).
Адреса: фронтенд `http://localhost:8080`, backend `http://localhost:5172`.

### Ключевые настройки
- `HealthCheckOptions` — веса категорий, штрафы, пороги, шкала (см. `Presenter/appsettings.json`).
- `ConnectionStrings:DefaultConnection` — PostgreSQL; `DatabaseOptions:AutoMigrate` включает миграции на старте.
- `SourceCraftServiceOptions`, `AppSecOptions`, `YandexIdOptions` — адреса и секреты источников.
- `SchedulingOptions` / `ScalingOptions` — расписание и параметры фоновой обработки.
- `DataProtectionStorageOptions` — файловое (по умолчанию) или S3-совместимое хранилище ключей.
- `AiOptions` / `AiModelsOptions` — провайдеры, базовые URL, модели и промпты; `AiTokenEncryptionOptions:Key` — ключ шифрования.

## 3. Используемые API и источники данных

| Источник | Где | Данные |
|---|---|---|
| SourceCraft REST (`https://api.sourcecraft.tech`, Refit, resilience-политики) | `infrastructure/SourceCraft/ISourceCraftApi.cs` | каталог, репозиторий, issues, merge requests, релизы, CI-запуски, `/user`, `/me/repos` |
| AppSec SourceCraft (`https://appsec.sourcecraft.tech`, Refit) | `infrastructure/SourceCraft/SourceCraftSecurityAdapter.cs` | находки SAST/SCA/secret scanning (`GET /v1/defect-groups`) |
| git (LibGit2Sharp, локальная копия) | `infrastructure/SourceCraft/LocalGitRepositoryReader.cs` | коммиты, контрибьюторы, TODO/FIXME, документация, структура, владельцы |
| Yandex ID OAuth (`https://oauth.yandex.ru`, `https://login.yandex.ru`) | `infrastructure/SourceCraft/YandexIdClient.cs` | вход и профиль пользователя |
| PostgreSQL (EF Core / Npgsql) | `infrastructure/Persistence/RepoHealthCheckerDbContext.cs` | репозитории, прогоны анализа, метрики, находки, рекомендации, пользователи |
| S3-совместимое хранилище (Minio SDK) | `infrastructure/DataProtection/S3XmlRepository.cs` | persistence ключей DataProtection (опционально; по умолчанию — файл) |
| Конфигурация `.env` | `infrastructure/Configuration/DotEnvEnvironmentFileLoader.cs` | значения настроек |

Сервис не импортирует репозитории с внешних платформ и не выполняет собственное
security-сканирование: security-оценка строится только на реальных находках AppSec SourceCraft.

## 4. Формула Repo Health Score

Расчёт — в `Application/HealthCheck/Services`, настройки — в `IOptions<HealthCheckOptions>`
(значения в `Presenter/appsettings.json`), без хардкода чисел.

1. **Нормализация метрики.** `MetricScore.NormalizedScore` — линейная нормализация сырого значения
   между «худшим» и «лучшим» в границах `ScoreScale` (0–100). Штрафные метрики нормализуются как
   `clamp(max − count × penalty, min, max)`.
2. **Балл категории.** Взвешенное среднее нормализованных оценок метрик со статусом `Available`:

   `CategoryScore = round( Σ(NormalizedScoreᵢ × Weightᵢ) / Σ(Weightᵢ), AwayFromZero )`

   Если все метрики категории без данных — категория получает статус `NoData` и `MinimumScore`.
3. **Итоговый балл.** Взвешенное среднее баллов доступных категорий:

   `Score = round( Σ(CategoryScoreᵢ × CategoryWeightᵢ) / Σ(CategoryWeightᵢ), AwayFromZero )`

   Категории со статусом `NoData`/`Unavailable` **исключаются**, веса доступных нормируются
   (отсутствие данных не ухудшает Score). Если доступных категорий нет — возвращается `MinimumScore` (0).

Веса категорий (`CategoryWeightsOptions`): Security 20%, Code health 20%, Activity 15%,
Documentation 15%, CI/CD 15%, Issues 15%. Методика версионируется (`MethodologyVersion`, сейчас `1.0`).

> Важная деталь устойчивости: балл считается **по метрикам** (объяснимо и воспроизводимо), а не
> по популярности. Лайки и активность не заменяют здоровье проекта; неполные данные не штрафуются.

## 5. Перечень и описание метрик

**Security** (`CalculateSecurity`, вес метрики = штраф):
- `SecurityCriticalFindings`, `SecurityHighFindings`, `SecurityMediumFindings`, `SecurityLowFindings` — число открытых находок по критичности (штрафы 25/15/8/3 по умолчанию);
- `SecurityFixedFindings` — информационная метрика исправленных находок, **вес 0** (не влияет на балл), нормализация `min + fixed × FixedFindingCredit`.

**Code health** (`CalculateCodeHealth`):
- `CodeHealthTodo` — число TODO (штраф 1);
- `CodeHealthFixme` — число FIXME (штраф 2);
- `CodeHealthStaleComments` — возраст самого старого комментария (дни); при превышении порога (180 дн.) применяется штраф 5.

**Activity** (`CalculateActivity`):
- `ActivityLastActivity` — дней с последней активности (коммит или дата репозитория);
- `ActivityCommitFrequency` — коммитов за окно `ActiveWithinDays`;
- `ActivityContributors` — контрибьюторы без ботов;
- `ActivityReleases` — опубликованные релизы;
- `ActivityMergeRequests` — добавляется только если данные collaboration доступны.

**Documentation** (`CalculateDocumentation`, бинарные метрики с весами):
- `DocumentationReadme` 0.30, `DocumentationLicense` 0.20, `DocumentationContributing` 0.10,
  `DocumentationCodeOwners` 0.10, `DocumentationLocalRun` 0.15, `DocumentationBuildAndTest` 0.15.

**CI/CD** (`CalculateCiCd`):
- `CiCdPresence` — число запусков пайплайнов;
- `CiCdSuccessRatio` — доля успешных среди завершённых;
- `CiCdPipelineDuration` — средняя длительность (мин).

**Issues** (`CalculateIssues`):
- `IssuesOpen`, `IssuesClosed`, `IssuesStale` (зависшие по `UpdatedAt` с откатом на `CreatedAt`);
- `IssuesFirstResponse` и `IssuesCloseTime` — среднее время ответа/закрытия; при отсутствии данных метрика помечается `NoData` и исключается из балла.

## 6. Правила обработки отсутствующих данных

`DataStatus` (Domain) имеет три значения: `Available`, `NoData`, `Unavailable`.

- **Комбинирование источников** (`AnalyzeRepositoryUseCase.Combine`): если хотя бы один источник
  `Unavailable` — итог `Unavailable`; если все `NoData` — `NoData`; иначе `Available`.
- **Категория без доступных метрик** получает `NoData` и `MinimumScore`, но её вес всё равно задан.
- **Итоговый Score** (`HealthScoreCalculator`) учитывает только `Available`-категории; их веса
  нормируются. `NoData` и `Unavailable` не ухудшают балл.
- **Security без AppSec** остаётся `NoData`; `RecommendationGenerator` добавляет явную
  рекомендацию «Подключите AppSec SourceCraft» (приоритет Low) без штрафа.
- **UI**: `NoData` → «Нет данных», `Unavailable` → «Источник недоступен» — разные подписи и иконки
  (`CategoryCapsules`, `CategoryPanel`). Отсутствующий балл показывается прочерком / «Нет данных»,
  а не как `0` и не как «ОПАСНО»; на графике сравнения отсутствующая категория даёт пропуск, а не `0`.
- Метрики с частичными данными (например, `IssuesFirstResponse`) помечаются `NoData` и исключаются
  из среднего, не превращаясь в ноль.

## 7. Алгоритм формирования рекомендаций

`RecommendationGenerator` (Application/HealthCheck/Services) формирует рекомендации на основе
уже посчитанных фактов:

1. Для каждой категории: если `DataStatus != Available` — пропуск (кроме Security, см. ниже);
   если `Score >= MinimumAcceptableScore` (70) — пропуск.
2. Приоритет: `Critical`, если балл < `CriticalPriorityScore` (25); `High`, если < `HighPriorityScore`
   (50); иначе `Medium`.
3. Черновик содержит: `Problem`, `WhyImportant`, `Evidence` (из `MetricScore.RawValue`: например
   «TODO — 47, самый старый комментарий — 200 дн.»), `Action`, `ExpectedImpact` и `SourceReference`.
4. `ExpectedImpact` считается как `round((70 − Score) × Weight / availableWeight)` — ожидаемый прирост
   итогового балла при доведении категории до приемлемого уровня.
5. `SourceReference` указывает источник и коды метрик (AppSec, SourceCraft Issues/Activity, файлы репозитория и т.п.).
6. Если Security недоступен — добавляется отдельная рекомендация подключить AppSec (Low).
7. `AnalyzeRepositoryUseCase` дополнительно добавляет рекомендации по аномалиям активности
   (`IAnomalyDetector`, приоритет High): всплески коммитов/MR, мелкие правки у ранее неактивных авторов.

## 8. Ограничения

- Оцениваются только репозитории, размещённые в **SourceCraft**; импорт с других платформ не делается.
- Security — **исключительно** из AppSec SourceCraft (SAST/SCA/secret scanning); без него категория —
  «Нет данных». Собственного сканирования нет.
- Штрафы, веса и пороги — настраиваемые эвристики (методика 1.0), а не абсолютная истина.
- Очередь анализа — **in-memory** (`ChannelAnalysisQueue`): задания не переживают рестарт и не общие
  для нескольких инстансов. Расписание защищено advisory-локом PostgreSQL, но воркер-очередь — нет.
- Полный сквозной прогон на крупном репозитории и граница «рабочая копия ≥ 500 МБ» требуют
  подтверждения на реальном репозитории (`SRHC_LARGE_REPO_PATH`).
- Приватные данные обрабатываются только в рамках прав авторизованного пользователя; токены
  SourceCraft/ИИ хранятся в зашифрованном виде (`AiTokenProtector`, `ISecretProtector`).
- AI-функции опциональны и требуют пользовательского ключа провайдера.

## 9. Подход к масштабированию

- **Stateless-инстансы**: состояние — в PostgreSQL, поэтому backend можно масштабировать репликами.
- **Планировщик**: `ScheduledAnalysisBackgroundService` использует `PeriodicTimer`
  (`RepositoriesRefreshMinutes`) и на каждый тик берёт advisory-лок PostgreSQL
  (`PostgresSchedulerLease`, `pg_try_advisory_lock`), поэтому одновременно считает и ставит задачи
  только одна реплика.
- **Воркеры**: `AnalysisWorkerBackgroundService` читает `IAnalysisQueue` (in-memory
  `ChannelAnalysisQueue`, ёмкость `ScalingOptions.QueueCapacity`) и обрабатывает задания с
  конкурентностью `WorkerConcurrency`, повторами `WorkerMaxAttempts` и задержкой `WorkerRetryDelayMilliseconds`.
- **Локальный кэш git-копий**: `GitWorkingCopyCache` держит клоны на диске с TTL
  (`GitCacheOptions.TtlMinutes`) и лимитом `MaxCachedRepositories`, освобождая место в фоне.
  Кэш локальный для инстанса.
- **Ключи DataProtection**: по умолчанию — локальный файл; при нескольких инстансах
  рекомендуется S3-совместимое хранилище (`DataProtectionStorageOptions.Bucket`), чтобы куки
  переживали рестарт и работали между репликами.
- **Направление роста**: для честного горизонтального масштабирования воркеров нужна внешняя
  durable-очередь (брокер) вместо in-memory канала; сейчас её нет.

## 10. Функции со звёздочкой (опциональные направления ТЗ)

Ни одно направление не подменяет обязательную часть. Ниже — каждое направление ТЗ и его статус.

| Направление | Статус | Где реализовано |
|---|---|---|
| Расширенный рейтинг, история и сравнение | Реализовано | `GET /api/repositories/{id}/history`, `GET /api/repositories/compare` (2–4 id), рейтинг `GET /api/repositories` |
| Углублённая аналитика (review, issues, bus factor, карта владельцев) | Реализовано | `GET /api/repositories/{id}/ownership` (`BusFactor`, `OwnerStat`), `GET /api/repositories/{id}/structure`, метрики Issues и Activity (MR/review) |
| Публичный API и quality badges | Реализовано | `GET /api/public/repositories/{id}/score`, `GET /api/public/repositories/{id}/badge.svg` (SVG-бейдж Score, кэш 5 мин) |
| AI-summary и AI-рекомендации | Реализовано | `POST /api/repositories/{id}/ai-summary`, `POST /api/repositories/{id}/ai-insights/{kind}` (+ SSE-стриминг `/stream`) |
| Защита публичного рейтинга от накрутки | Реализовано | `GET /api/repositories/{id}/integrity` (`RepositoryIntegrityCalculator`, `IAnomalyDetector`), индекс достоверности и флаг в рейтинге |

Детали по направлениям:

- **Расширенный рейтинг и сравнение.** Публичный рейтинг (`/rating`, `GET /api/repositories`) с
  фильтром по языкам, сортировками по Score/лайкам/активности и постраничной навигацией; история
  Score для графика динамики; сравнение 2–4 репозиториев по Score, категориям и метрикам.
- **Углублённая аналитика.** Bus factor и карта владельцев считаются по git-истории
  (`GitOwnershipReader`, `OwnershipOptions.CoverageThresholdPercent`); Issues — открытые/закрытые/
  зависшие и время реакции; review-активность — через merge requests (`ActivityMergeRequests`,
  `ActivityMergeRequestResponse`, `MergeRequestInfo.ReviewCommentsCount`); папки — `structure`/`folders`.
- **Публичный API и badge.** Кроме публичного JSON-рейтинга и анализа, есть отдельные публичные
  read-only эндпоинты score и SVG-бейджа: `badge.svg` рендерит `RepositoryBadgeSvgRenderer` по
  текущему Score, приватные репозитории отдают 404, ответ кэшируется (`Cache-Control: max-age=300`).
- **AI-summary и AI-рекомендации.** Единая фабрика `IChatClientFactory` (см. раздел 11): резюме,
  рекомендации, объяснение, план улучшения, триаж безопасности и прогноз рисков; ответ можно
  стримить (SSE) с индикатором «Думает» и отображением «размышлений» reasoning-моделей.
- **Защита от накрутки.** `RepositoryIntegrityCalculator` комбинирует аномалии активности
  (`IAnomalyDetector`: всплески коммитов/MR, мелкие правки у ранее неактивных авторов),
  «пустые» коммиты и лайки без активности; результат — статус (`ok`/`suspicious`/`check`) и
  штрафные сигналы. Индекс честности показывается в рейтинге флагом, но **не** подменяет Score.
- **Дополнительно:** отчёты в форматах Markdown, JSON, HTML и PDF; live-статус анализа через
  WebSocket `/ws/analysis`; страницы методики и честные подписи «Нет данных».

## 11. Внешние сервисы и ИИ

- **SourceCraft REST / AppSec SourceCraft** — REST через Refit; для HTTP-клиентов включены retry,
  timeout и circuit breaker (standard resilience handler). PAT/service-токен передаётся авторизацией.
- **Yandex ID OAuth** — вход по Я ID; выдаётся подписанный тикет-куки; PAT пользователя хранится
  зашифрованно и используется только в рамках его прав.
- **git (LibGit2Sharp)** — локальный клон/чтение истории; исходный код не хранится дольше необходимого
  (см. локальный кэш с TTL).
- **PostgreSQL** — хранение данных и advisory-лок планировщика.
- **S3-совместимое хранилище (Minio SDK)** — опциональное persistence ключей DataProtection.
- **ИИ**: единая фабрика `IChatClientFactory` (`OpenAiChatClientFactory`) поверх OpenAI-совместимого
  ChatClient. Поддерживаются провайдеры **OpenAI, Anthropic, Google Gemini, Yandex, DeepSeek**
  (базовые URL в `AiOptions`). Провайдер, модель и токен выбирает пользователь; токен шифруется
  (`AiTokenProtector` / `AiTokenEncryptionOptions:Key`). Модель получает **только факты и метрики**
  анализа, а промпты (резюме, рекомендации, объяснение, план, триаж, прогноз) задаются в `AiOptions`
  и требуют опоры на данные без выдумок.
