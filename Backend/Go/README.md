# Go-сервис SourceCraft

Обёртка над [публичным REST API SourceCraft](https://api.sourcecraft.tech/docs/index.html) и временное хранилище git-репозиториев в S3-совместимом хранилище. Требования — в [TZ.md](TZ.md).

## Структура

| Пакет | Что делает |
|---|---|
| `cmd/server` | точка входа, HTTP-сервер (локально и в Serverless Container) |
| `internal/sourcecraft` | клиент API: ретраи с backoff, `Retry-After`, rate limit, пагинация `page_token` |
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
| `GET /repositories/{id}/security/findings` | `ISourceCraftSecuritySource` | всегда `Unavailable` (см. ниже) |
| `GET /repositories/{id}/code-health?runId` | `ISourceCraftCodeHealthSource` | git grep + git blame; нужен снапшот `withBlobs: true` |
| `GET /repositories/{id}/documentation?runId` | `ISourceCraftDocumentationSource` | файлы ветки + разбор README/CONTRIBUTING; нужен снапшот `withBlobs: true` |
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
- **Без `runId`**: activity-эндпоинты делают эфемерный клон только на время запроса и не трогают S3.
- **Git-креды**: `http.extraHeader` через `GIT_CONFIG_*` env. Токена нет ни в remote URL, ни в argv, ни в логах.
- **Лимиты**: `GIT_MAX_REPO_MB` (размер клона и распаковки) и `GIT_CLONE_TIMEOUT`. При превышении — `Unavailable`.

## Code health и документация

Оба эндпоинта читают содержимое файлов, поэтому снапшот нужно создать с `{"withBlobs": true}` (без `depth`, иначе возраст TODO обрежется границей истории). На снапшоте без блобов ответ — `409 snapshot_without_blobs`, а не тихий ноль. Без `runId` сервис делает эфемерный клон: полный для code-health, `--depth 1` для документации.

**Code health** (`CodeHealthReport`) — по ветке по умолчанию:
- маркеры `TODO`, `FIXME`, `HACK`, `XXX` — отдельным словом в верхнем регистре (`TODOS`, `todo_list` не считаются), только текстовые файлы;
- `todoCount`, `fixmeCount` — по типам; `totalCommentCount` — все четыре маркера;
- исключены `vendor/`, `node_modules/`, `third_party/`, минифицированные файлы, lock-файлы, `go.sum`, `*.svg`, `*.map` — это не долг проекта;
- `oldestCommentAge` — возраст самой старой строки с маркером по `git blame` (TimeSpan `d.hh:mm:ss`), `null` если маркеров нет. Если файлов с маркерами больше 500, blame идёт по равномерной выборке из 500 файлов и возраст — оценка.

**Документация** (`DocumentationReport`):
- `hasReadme` — `README*` в корне или `docs/`, `.sourcecraft/`, `.github/`;
- `hasLicense` — `LICENSE*`, `LICENCE*`, `COPYING*` в корне;
- `hasContributing`, `hasCodeOwners` — `CONTRIBUTING*`, `CODEOWNERS` в корне, `docs/`, `.sourcecraft/`, `.github/`, `.gitlab/`;
- `hasLocalRunInstructions`, `hasBuildAndTestInstructions` — по README и CONTRIBUTING: команда в блоке кода (`docker compose up`, `go run`, `npm start`, `go test`, `pytest`…) или заголовок раздела («Quick start», «Запуск», «Сборка», «Тесты»…) при наличии блока кода. Слова в обычном тексте не засчитываются — так один абзац «run, build, test» не накручивает оценку. Для `buildAndTest` нужны и сборка, и тесты.

Замер: на синтетическом репозитории из 10 000 файлов grep занимает ~0,6 с, blame — ~8 мс на файл без глубокой истории.

## Ограничения API SourceCraft (на 2026-09-25)

- **AppSec** (SAST, SCA, secret scanning) в публичном API отсутствует, поэтому `security/findings` честно отдаёт `Unavailable` вместо имитации сканирования.
- **Коммитов** в API нет. `activity/commits` и `activity/contributors` считаются по git-истории. Поэтому `Contributor.Login` — имя автора из git; авторы объединяются по email.
- **Merge request**: у PR нет `merged_at` и `closed_at`. Для завершённых PR берётся `updated_at`.
- **CI**: у запусков нет ветки, `PipelineRun.Branch` пустой. У запусков нет публичного id, вместо него используется `slug`.
- **Профиль пользователя** не отдаёт email, `SourceCraftUser.Email` всегда `null`.
- **Лайки**: `LikesCount` — сумма всех rating-реакций (Like, Heart, Diamond).
- **Яндекс ID не даёт доступа к API SourceCraft.** API принимает только PAT и IAM-токены Yandex Cloud, а обмен OAuth-токенов Яндекса на IAM закрыт для новых токенов с 01.06.2026. Поэтому вход через Яндекс ID только устанавливает личность, токен Яндекса сразу отбрасывается. Для приватных репозиториев (`/auth/me`, `/auth/repositories`, снапшоты) пользователь по-прежнему передаёт свой PAT SourceCraft.
