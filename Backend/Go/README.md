# Go-сервис SourceCraft

Обёртка над [публичным REST API SourceCraft](https://api.sourcecraft.tech/docs/index.html) и временное хранилище git-репозиториев в S3-совместимом хранилище. Требования — в [TZ.md](TZ.md).

## Структура

| Пакет | Что делает |
|---|---|
| `cmd/server` | точка входа, HTTP-сервер (локально и в Serverless Container) |
| `cmd/function` | точка входа Cloud Functions: serverless-обработчик с триггерами |
| `internal/serverless` | сборка serverless-обработчика: API + очередь + таймер |
| `internal/queue` | потребитель триггера Message Queue (создание снапшотов) |
| `internal/sourcecraft` | клиент API: ретраи с backoff, `Retry-After`, rate limit, пагинация `page_token`, circuit breaker |
| `internal/appsec` | клиент AppSec API SourceCraft (`/v1/defect-groups`): те же ретраи и пагинация |
| `internal/gitrepo` | bare partial clone, потоковый `git log`; креды только через env процесса git |
| `internal/objectstore` | интерфейс S3 + реализация на `minio-go` (Yandex Object Storage, MinIO, AWS) |
| `internal/snapshot` | жизненный цикл снапшота репозитория в S3 |
| `internal/service` | маппинг фактов SourceCraft в C#-модели |
| `internal/httpapi` | REST-эндпоинты, конверт ответа, маппинг ошибок |
| `internal/contract` | JSON-контракт с C# (`Application/SourceCraft/Models`) |

## Запуск

```bash
cp .env.example .env          # вписать SOURCECRAFT_TOKEN
docker compose up --build     # сервис на :8080 + MinIO на :9000 (консоль :9001)
```

Без Docker: поднять любое S3-хранилище, выставить переменные из `.env.example` и `go run ./cmd/server`.

Тесты: `go test -race ./...` (нужен установленный `git`).

## Эндпоинты

Токен пользователя SourceCraft передаётся в `Authorization: Bearer <PAT>`. Без него используется сервисный `SOURCECRAFT_TOKEN`.

| Метод и путь | Порт C# | Источник |
|---|---|---|
| `POST /auth/url` `{"state":"…"}` → `{"url":"…"}` | `GetAuthorizationUrlAsync` | ссылка на вход через Яндекс ID |
| `POST /auth/token` `{"code":"…","state":"…"}` → `SourceCraftUser` | `CompleteAuthorizationAsync` | Яндекс ID: обмен кода, `login.yandex.ru/info` |
| `GET /auth/me`, `GET /auth/repositories` | `ISourceCraftAuthentication` | API `/user`, `/me/repos` |
| `GET /repositories?pageToken&pageSize&sortBy` | `ISourceCraftRepositoryCatalog` | API `/repos` (публичный каталог) |
| `GET /repositories/{id}` | `ISourceCraftRepositoryCatalog` | API `/repos/id:{id}` |
| `GET /repositories/{id}/activity/commits?runId` | `ISourceCraftActivitySource` | git log |
| `GET /repositories/{id}/activity/contributors?runId` | `ISourceCraftActivitySource` | git log |
| `GET /repositories/{id}/activity/releases` | `ISourceCraftActivitySource` | API releases |
| `GET /repositories/{id}/issues` | `ISourceCraftCollaborationSource` | API issues + comments |
| `GET /repositories/{id}/merge-requests` | `ISourceCraftCollaborationSource` | API pulls + comments |
| `GET /repositories/{id}/pipelines` | `ISourceCraftPipelineSource` | API `cicd/runs` |
| `GET /repositories/{id}/code-health?runId` | `ISourceCraftCodeHealthSource` | git: TODO/FIXME и давность самого старого |
| `GET /repositories/{id}/documentation?runId` | `ISourceCraftDocumentationSource` | git: README/LICENSE/CONTRIBUTING/CODEOWNERS/инструкции |
| `GET /repositories/{id}/security/findings` | `ISourceCraftSecuritySource` | AppSec API `/v1/defect-groups` (см. ниже) |
| `PUT /repositories/{id}/snapshots/{runId}` | — | клон → S3; тело `{"withBlobs":false,"depth":0}` необязательно |
| `GET /repositories/{id}/snapshots/{runId}` | — | метаданные снапшота |
| `DELETE /repositories/{id}/snapshots/{runId}` | — | удаление снапшота |
| `POST /internal/snapshots/reap` | — | очистка просроченных снапшотов; заголовок `X-Internal-Token` |

Каждый ответ приходит в конверте:

```json
{ "requestId": "…", "collectedAt": "2026-09-25T15:00:00Z", "status": "Available", "data": { … }, "reason": "…" }
```

- `status`: `Available`, `NoData` (пустой список, нет коммитов) или `Unavailable` (источник упал или репозиторий превысил лимит). Сбой одного источника отвечает `200` со статусом `Unavailable` и не роняет остальные категории.
- HTTP-коды: `400`, `401`, `403`, `404`, `504` — по разделу 6 ТЗ.
- Поля в camelCase, enum'ы строками. На стороне C# нужен `JsonStringEnumConverter`.

## Вход через Яндекс ID

1. Зарегистрировать приложение на [oauth.yandex.ru](https://oauth.yandex.ru/client/new) (платформа «Веб-сервисы», права `login:info`, `login:email`), указать Redirect URI — callback C#-бэкенда. Вписать `YANDEX_CLIENT_ID`, `YANDEX_CLIENT_SECRET`, `YANDEX_REDIRECT_URI`. Без них `/auth/url` и `/auth/token` отвечают `501`.
2. C# генерирует случайный `state`, сохраняет его в сессии и вызывает `POST /auth/url`. Пользователь уходит по полученной ссылке.
3. Яндекс возвращает пользователя на callback C# с `code` и `state`. **C# сверяет `state` с сессией** (защита от CSRF; Go-сервис stateless и сверить не может) и вызывает `POST /auth/token`.
4. Ответ — `SourceCraftUser` с `id` из Яндекс ID. Код одноразовый и живёт 10 минут; повторный или просроченный код даёт `400 invalid_grant`.

## Снапшоты репозитория в S3

Типичный прогон анализа:

```text
PUT    /repositories/r1/snapshots/run-42        # 201 — клон в S3; повтор — 200, без повторного клона
GET    /repositories/r1/activity/commits?runId=run-42
GET    /repositories/r1/activity/contributors?runId=run-42
DELETE /repositories/r1/snapshots/run-42        # 204, идемпотентно
```

- **Формат**: `git clone --bare --single-branch --no-tags --filter=blob:none` по ветке по умолчанию. Хранится только история и деревья, содержимого файлов нет. `withBlobs: true` даёт полный клон ветки, `depth` — shallow-клон.
- **Ключ**: `{S3_PREFIX}/{repositoryId}/{runId}/repo.tar.gz`. Параллельные прогоны не пересекаются.
- **Загрузка**: tar.gz упаковывается потоком прямо в multipart upload частями по 16 МБ, без промежуточного файла. Локальный клон удаляется сразу после загрузки.
- **Шифрование**: SSE-S3 (`S3_SSE=true`).
- **Удаление** снапшота гарантируют четыре механизма:
  1. явный `DELETE` после анализа;
  2. метаданные `expires-at` (TTL `SNAPSHOT_TTL`, по умолчанию 3 часа): просроченный снапшот не читается;
  3. reaper — `POST /internal/snapshots/reap` по таймер-триггеру или `SNAPSHOT_REAP_INTERVAL` локально;
  4. lifecycle-правило бакета (`S3_LIFECYCLE_DAYS`, по умолчанию 1 день) и очистка незавершённых multipart-загрузок.
- **Доступ**: перед каждой операцией со снапшотом сервис проверяет через API, что токен запроса видит репозиторий. Чужой снапшот по известному `runId` не прочитать.
- **Без `runId`**: activity-эндпоинты делают эфемерный клон только на время запроса и не трогают S3. `code-health` и `documentation` клонируют с блобами (нужно содержимое файлов); для `runId` создавайте снапшот с `{"withBlobs":true}`.
- **Git-креды**: `http.extraHeader` через `GIT_CONFIG_*` env. Токена нет ни в remote URL, ни в argv, ни в логах.
- **Лимиты**: `GIT_MAX_REPO_MB` (размер клона и распаковки) и `GIT_CLONE_TIMEOUT`. При превышении — `Unavailable`.

## Serverless

Раздел 4 ТЗ: часть Go-сервиса разворачивается как Cloud Functions, тяжёлый анализ — как Serverless Container (тот же код, `cmd/server`). Точка входа функции — `cmd/function`; она поднимает тот же набор маршрутов, что и обычный сервер, плюс триггеры, и подходит для локального запуска.

```bash
go run ./cmd/function            # :8080, HTTP_ADDR/PORT
```

`serverless.NewHandler` собирает полный граф (S3, git, снапшоты, сервис, Яндекс ID) и внешний `http.ServeMux`:

| Метод и путь | Назначение | Защита |
|---|---|---|
| `POST /internal/queue/messages` | триггер Message Queue: `service.CreateSnapshot` по каждому сообщению | `X-Internal-Token` |
| `POST /internal/snapshots/reap` | таймер-триггер: очистка просроченных снапшотов (`svc.ReapSnapshots`) | `X-Internal-Token` |
| остальные маршруты | `internal/httpapi` (раздел 2 ТЗ) | по маршруту |

**Триггер Message Queue.** Тело — конверт триггера Yandex Message Queue:

```json
{"messages":[{"details":{"message":{"message_id":"…","body":"{\"repositoryId\":\"r1\",\"runId\":\"run-42\",\"withBlobs\":false,\"depth\":0}"}}}]}
```

Обработка должна быть идемпотентной: `CreateSnapshot` по `(repositoryId, runId)` не клонирует повторно. Ответ — по каждому сообщению:

```json
{"results":[{"messageId":"…","repositoryId":"r1","runId":"run-42","status":"created","created":true}]}
```

`status`: `created` (снапшот создан), `exists` (уже был) или `failed`. Если хотя бы одно сообщение не обработано, ответ `502` — очередь повторит доставку (успешные сообщения переигрываются идемпотентно). Битая пачка — `400`, нет внутреннего токена — `403`.

**Таймер-триггер.** Вызывает `POST /internal/snapshots/reap`; тот же путь есть в `internal/httpapi` для `cmd/server`, а `cmd/function` обслуживает его через `internal/serverless` (`timer.go`) — без изменения поведения.

**Circuit breaker.** Клиент SourceCraft оборачивается в потокобезопасный `http.RoundTripper` (`internal/sourcecraft/breaker.go`): после `SOURCECRAFT_BREAKER_THRESHOLD` подряд идущих сбоев (ошибка транспорта, `5xx`, `429`) запросы отклоняются на `SOURCECRAFT_BREAKER_COOLDOWN`, затем пропускается одна пробная попытка. `4xx` сбоем не считается. В обычном сервере (`cmd/server`) breaker не включается.

| Переменная | По умолчанию | Значение |
|---|---|---|
| `HTTP_ADDR` | `:8080` | адрес прослушивания; приоритетнее `PORT` |
| `PORT` | — | порт Cloud Functions, если `HTTP_ADDR` не задан |
| `SOURCECRAFT_BREAKER_THRESHOLD` | `5` | сбоев подряд до размыкания цепи |
| `SOURCECRAFT_BREAKER_COOLDOWN` | `30s` | пауза, пока цепь разомкнута |

## AppSec (SAST/SCA/secret scanning)

Security-оценка строится только на реальных находках [AppSec SourceCraft](https://appsec.sourcecraft.tech/openapi); собственное сканирование не имитируется. `GET /repositories/{id}/security/findings` сначала разрешает репозиторий через SourceCraft, затем запрашивает группы дефектов у AppSec API.

- **Эндпоинт:** `GET {APPSEC_API_URL}/v1/defect-groups` с `gitRepo` = внутренний `id` репозитория из `GET /repos/id:{id}` (не slug). Токен запроса уходит в `Authorization: Bearer`.
- **Пагинация:** `pageSize` до 250, обход `nextPageToken` до 100 страниц; повтор одного и того же токена обрывает обход.
- **Ретраи:** транспорт, `5xx`, `429`; учитывается `Retry-After` (как у клиента SourceCraft). Токен не логируется.
- **Маппинг в контракт C#:**
  - `Kind`: `engineType`/`engine`: `SECRETS` → `SecretScanning`, `SCA` → `Sca`, `SAST`/`DAST`/`AI_AUDIT`/прочее → `Sast`.
  - `Severity`: `0 NONE → Low`, `1 LOW → Low`, `2 MEDIUM → Medium`, `3 HIGH → High`, `4 CRITICAL → Critical` (в контракте нет `None`, поэтому `NONE` понижается до `Low`).
  - `Status`: `0 OPEN → Open`, иначе → `Fixed`.
  - `Title` = `ruleName` (при пустом — `ruleId`), `Package` = `ruleId`, `FilePath` = `fileName`, `Id` = `uuid` (при пустом — `publicId`).
- **Нет данных:** репозиторий без находок — `NoData`; уже разрешённый репозиторий, которого AppSec не знает (`404`), или сбой AppSec — `Unavailable` с причиной, без подстановки фиктивных данных.

| Переменная | По умолчанию | Значение |
|---|---|---|
| `APPSEC_API_URL` | `https://appsec.sourcecraft.tech` | корень AppSec API |
| `APPSEC_TIMEOUT` | `15s` | таймаут одного запроса |
| `APPSEC_MAX_RETRIES` | `3` | повторы транспорта, `5xx`, `429` |

Порядок `engineType`, `severity` и `status` — предположение по OpenAPI; при изменении схемы правится в `service.toFindingKind`/`toFindingSeverity`/`toFindingStatus`.

## Ограничения API SourceCraft (на 2026-09-25)

- **AppSec**: находки берутся из отдельного AppSec API (`/v1/defect-groups`), а не из публичного REST API SourceCraft (см. раздел «AppSec» выше). Имитация сканирования не используется.
- **Коммитов** в API нет. `activity/commits` и `activity/contributors` считаются по git-истории. Поэтому `Contributor.Login` — имя автора из git; авторы объединяются по email.
- **Merge request**: у PR нет `merged_at` и `closed_at`. Для завершённых PR берётся `updated_at`.
- **CI**: у запусков нет ветки, `PipelineRun.Branch` пустой. У запусков нет публичного id, вместо него используется `slug`.
- **Профиль пользователя** не отдаёт email, `SourceCraftUser.Email` всегда `null`.
- **Лайки**: `LikesCount` — сумма всех rating-реакций (Like, Heart, Diamond).
- **Яндекс ID не даёт доступа к API SourceCraft.** API принимает только PAT и IAM-токены Yandex Cloud, а обмен OAuth-токенов Яндекса на IAM закрыт для новых токенов с 01.06.2026. Поэтому вход через Яндекс ID только устанавливает личность, токен Яндекса сразу отбрасывается. Для приватных репозиториев (`/auth/me`, `/auth/repositories`, снапшоты) пользователь по-прежнему передаёт свой PAT SourceCraft.
