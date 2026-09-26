# Возможности проекта

Статусы: **✅ по ТЗ** — обязательное; **⭐ сверх ТЗ** — дополнительные улучшения; **❌ не сделано** — незакрытые пункты ТЗ.

## Ядро оценки здоровья

- ✅ **Repo Health Score 0–100** с детализацией по категориям, воспроизводимый и объяснимый. Веса: Security 20%, Code health 20%, Activity 15%, Documentation 15%, CI/CD 15%, Issues 15% (`CategoryWeightsOptions`).
- ✅ **Шесть категорий**: Security (AppSec SourceCraft), Code health (TODO/FIXME/старый техдолг), Activity (коммиты, contributors, релизы, MR), Documentation (README/LICENSE/CONTRIBUTING/CODEOWNERS/инструкции), CI/CD (пайплайны), Issues.
- ✅ **Нормализация метрик** линейная в границах `ScoreScaleOptions`; балл категории — взвешенное среднее метрик (числа объяснения сходятся).
- ✅ **«Нет данных»** — отдельный статус, не плохой результат; веса доступных категорий нормируются.
- ✅ **Контрольные сценарии методики** (тесты): проблемы заметно снижают Score, исправления повышают, NoData не ухудшает, второстепенные показатели не доминируют, популярность не подменяет здоровье.
- ✅ **Рекомендации** с фактами из метрик, «почему важна», «подтверждающие факты», приоритетом, **количественным** ожидаемым влиянием и `SourceReference`.

## Данные SourceCraft

- ✅ Сбор через **API SourceCraft** (каталог, issues, MR, CI), **git** (история, коммиты, TODO/FIXME, структура) и **AppSec API** (`appsec.sourcecraft.tech`: SAST/SCA/secrets).
- ✅ **Устойчивость**: ретраи, circuit breaker, rate limiting, деградация категории до `Unavailable`.
- ✅ **S3-совместимое хранилище** (Yandex Object Storage) для снапшотов репозиториев с TTL и автоочисткой.
- ✅ Я ID OAuth (Go-сервис) + C#-поток авторизации.
- ⭐ **Детект аномалий активности** (`AnomalyDetectionOptions`): всплеск коммитов/MR от нового автора за короткий срок; небольшие изменения от ранее неактивного автора. Формируются рекомендации.
- ⭐ **Health Check по папкам** (`GET /repositories/{id}/structure`): глубина, число файлов/каталогов, корневые файлы, самая крупная папка — без проверки корневых README/LICENSE.

## Пользовательские сценарии

- ✅ **Публичный рейтинг**: место, проект, ссылка, Score, лайки, язык, последняя активность; фильтр по языку (регистронезависимый), сортировки по Score/лайкам/активности, пагинация; приватные репозитории исключены.
- ✅ **Страница анализа**: Score, категории, сильные/слабые стороны, объяснение расчёта (метрики), рекомендации, «Нет данных», дата последнего анализа.
- ✅ **Свой репозиторий через Я ID**: вход, хранение PAT (шифрование), список доступных репозиториев, запуск анализа; доступ к приватным данным — только владельцу.
- ✅ **Периодический пересчёт**: планировщик + очередь задач + воркеры (роли `web`/`worker`, распределённая аренда).
- ✅ **Выгрузка отчёта**: Markdown, JSON, HTML, PDF (`GET /api/repositories/{id}/report?format=...`).

## Serverless и эксплуатация

- ✅ **Serverless-развёртывание (Yandex Cloud)**: stateless Go (Serverless Containers/Functions), Message Queue-хендлер, timer-триггер, circuit breaker; C# — ASP.NET в контейнере.
- ✅ **DI**: Microsoft.Extensions.Hosting + Scrutor + extension-методы (`AddApplication`/`AddInfrastructure`).
- ✅ **EF Core + PostgreSQL**: миграции, авто-миграция (`DatabaseOptions.AutoMigrate`), `EnableRetryOnFailure`.
- ✅ **Ключи DataProtection** персистятся в S3-совместимом хранилище (переживают холодный старт).
- ✅ **Секреты**: fail-fast ключ шифрования токенов; секреты — через env/user-secrets.
- ✅ **Логирование**: Serilog (красивый консольный вывод, async) через абстракцию `ILogger<T>`; sink в Yandex Cloud Logging.
- ✅ **Глобальная обработка ошибок** → корректные HTTP-коды.
- ⭐ **CI**: GitHub Actions и SourceCraft CI (`.sourcecraft/ci.yaml`), общие PowerShell-скрипты в `Scripts/`.
- ⭐ **Проверки размера репозитория**: `check-file-count.ps1`, `check-repo-size.ps1`, `check-repo-health.ps1`.
- ⭐ **Крупные репозитории**: partial clone, потоковый `git log`, лимиты размера/времени, bounded `git`-команды; gated-тесты (`LARGE_REPO_PATH`, `LARGE_REPO_TESTS`); проверено на `microsoft/vscode` (166 240 коммитов / 19 427 файлов).

## ИИ (сверх ТЗ)

- ⭐ **AI-summary** через Microsoft.Extensions.AI; провайдеры из `AiProviders`: OpenAI, Ollama, Anthropic, GoogleGemini, **Yandex**, XAi, DeepSeek (OpenAI-совместимые base URL; Yandex — `llm.api.cloud.yandex.net`). Пользователь выбирает провайдера, настройки хранятся зашифрованно (`PUT /api/me/ai`, `POST /api/repositories/{id}/ai-summary`).

## Не сделано / ограничения

- ❌ **CLI SourceCraft** не используется (только API + git + AppSec API). Нужно задействовать или обосновать эквивалентность.
- ❌ **Полный сквозной прогон на крупном репо** (все 6 категорий → Score → БД) и граница **≥500 МБ рабочей копии** не подтверждены end-to-end (проверены только git-операции Go).
- ❌ **Маппинг AppSec** `severity/status/engineType` (int→enum) не проверен на реальных данных.
- ❌ **Фронтенд**: только серверный HTML (рейтинг/анализ); нет SPA, формы ввода PAT и выбора провайдера.
- ❌ **Автодеплой/демо-стенд** и размещение в SourceCraft — организационное.
- ⭐ Часть со звёздочкой по ТЗ (история/сравнение, quality badges, публичный API, AI-рекомендации как отдельная функция, защита рейтинга от накрутки сверх детекта аномалий) — не реализована.

## Запуск (кратко)

- C#: `dotnet build SourceCraftRepoHealthChecker.slnx`; запуск `Presenter` (нужны `ConnectionStrings:DefaultConnection`, `AiTokenEncryptionOptions:Key`, ключи DataProtection, `SourceCraftServiceOptions:BaseUrl` — Go-сервис).
- Go: `cd Backend/Go && go run ./cmd/server` (или docker compose).
- Тесты: `dotnet test ...` и `go test ./...` (или `Scripts/*.ps1`).
- Публичный API и эндпоинты — см. `Docs/API.md`.
