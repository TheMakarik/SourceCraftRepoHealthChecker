# API и внутренние контракты

Документ описывает:

1. HTTP-эндпоинты C#-бэкенда (`Presenter`) — публичная поверхность.
2. Внутренний контракт C#↔Go: эндпоинты Go-сервиса и конверт ответа.

## 1. HTTP-эндпоинты Presenter

Точка входа — `Backend/CSharp/SourceCraftRepoHealthChecker.Presenter/Program.cs`. По умолчанию сервис слушает `http://localhost:5172` (профиль `http` в `Properties/launchSettings.json`).

| Метод и путь | Параметры | Ответ | Назначение |
|---|---|---|---|
| `GET /healthz` | — | `200 {"status":"ok"}` | проверка живости |
| `POST /api/repositories/refresh` | — | `200 {"refreshed": <int>}` | обновить каталог открытых репозиториев из SourceCraft |
| `GET /api/repositories` | `language?`, `sort?`, `page?`, `pageSize?` | `RepositoryLeaderboardPage` (JSON) | публичный рейтинг |
| `GET /api/repositories/{id}/analysis` | — | `RepositoryAnalysis` (JSON) или `404` | последний завершённый анализ репозитория из БД |
| `GET /api/repositories/{id}/report.md` | — | `text/markdown; charset=utf-8` или `404` | Markdown-отчёт |
| `GET /rating` | `language?`, `sort?`, `page?` | HTML (`text/html`) | страница рейтинга |
| `GET /repositories/{id}` | — | HTML (`text/html`) или `404` | страница анализа |

Пояснения:

- `sort`: `score` (по умолчанию), `likes`, `activity`. Неизвестное значение трактуется как `score`.
- `page` по умолчанию 1; `pageSize` по умолчанию 20, ограничен значением от 1 до 100 (`GetRepositoryLeaderboardUseCase.MaximumPageSize`).
- `GET /api/repositories` возвращает `RepositoryLeaderboardPage`: `{ items[], totalCount, page, pageSize }`, где элемент — `{ place, sourceCraftId, name, fullName, url, score, likesCount, language, lastActivityAt, analyzedAt }`. `score`/`analyzedAt` — из последнего **завершённого** прогона анализа (`AnalysisStatus.Completed`), иначе `null`; в HTML-странице такой Score показывается как «Нет данных».
- `GET /api/repositories/{id}/analysis` читает **сохранённый** анализ (`RepositoryAnalysis`: репозиторий, Score, дата последнего анализа, категории, сильные/слабые стороны, рекомендации). Если репозитория или завершённого прогона нет — `404`.
- Запуск нового анализа выполняет `IAnalyzeRepositoryUseCase` (`AnalyzeRepositoryUseCase`): он собирает факты из всех портов SourceCraft, считает Score и сохраняет `AnalysisRun`. **Отдельного HTTP-эндпоинта для запуска анализа сейчас нет** — use case зарегистрирован в DI и используется программно/тестами.
- Выгрузка отчёта реализована в формате **Markdown** (рендер `RepositoryReportMarkdownRenderer`). PDF нет.

### Аутентификация (Я ID)

Поток входа:

1. C# генерирует случайный `state` и сохраняет его в сессии (защита от CSRF).
2. C# вызывает Go `POST /auth/url` `{"state":…}` и получает `{"url":…}`; пользователь уходит по ссылке.
3. Яндекс возвращает пользователя на callback C# с `code` и `state`; C# сверяет `state` с сессией.
4. C# вызывает Go `POST /auth/token` `{"code":…,"state":…}`; Go обменивает код на профиль (`login.yandex.ru/info`) и возвращает `SourceCraftUser` (id, login, displayName, email).
5. C# заводит/обновляет `User` по `YaId` и сохраняет в БД.

Токен Яндекса не даёт доступа к API SourceCraft, поэтому сразу отбрасывается; для приватных репозиториев пользователь передаёт свой PAT SourceCraft (см. [data-sources.md](data-sources.md)).

Состояние реализации:

- В `Application` объявлены порт `ISourceCraftAuthentication` и use case'ы `IAuthenticateUserUseCase` (`StartAsync`, `CompleteAsync`), `IGetUserRepositoriesUseCase`; в `infrastructure` — адаптер `SourceCraftAuthenticationAdapter` (ходит в Go) и `SourceCraftAccessTokenAccessor` (PAT текущего пользователя в `AsyncLocal`).
- **HTTP-маршрутов авторизации в `Presenter` на момент написания нет**: шаги 1–5 выше описаны на уровне Application/Go и требуют маппинга маршрутов (URL, callback, список репозиториев) на стороне C#.

## 2. Внутренний контракт C#↔Go

C# обращается к Go через `SourceCraftHttpClient` (`infrastructure/SourceCraft`). Базовый адрес — `SourceCraftServiceOptions.BaseUrl`.

### Эндпоинты Go

Токен пользователя SourceCraft передаётся в заголовке `Authorization: Bearer <PAT>`. Без него используется сервисный `SOURCECRAFT_TOKEN`.

| Метод и путь | Порт C# / назначение | Источник |
|---|---|---|
| `POST /auth/url` `{"state":…}` → `data: {"url":…}` | `GetAuthorizationUrlAsync` | ссылка на вход через Я ID |
| `POST /auth/token` `{"code":…,"state":…}` → `data: SourceCraftUser` | `CompleteAuthorizationAsync` | Я ID: обмен кода, `login.yandex.ru/info` |
| `GET /auth/me` | `GetCurrentUserAsync` | API `/user` |
| `GET /auth/repositories` | `GetAvailableRepositoriesAsync` | API `/me/repos` |
| `GET /repositories?pageToken&pageSize&sortBy` | `ISourceCraftRepositoryCatalog` | API `/repos` (публичный каталог) |
| `GET /repositories/{id}` | `ISourceCraftRepositoryCatalog` | API `/repos/id:{id}` |
| `GET /repositories/{id}/activity/commits?runId` | `ISourceCraftActivitySource` | git log |
| `GET /repositories/{id}/activity/contributors?runId` | `ISourceCraftActivitySource` | git log |
| `GET /repositories/{id}/activity/releases` | `ISourceCraftActivitySource` | API releases |
| `GET /repositories/{id}/issues` | `ISourceCraftCollaborationSource` | API issues + comments |
| `GET /repositories/{id}/merge-requests` | `ISourceCraftCollaborationSource` | API pulls + comments |
| `GET /repositories/{id}/pipelines` | `ISourceCraftPipelineSource` | API `cicd/runs` |
| `GET /repositories/{id}/code-health?runId` | `ISourceCraftCodeHealthSource` | git: TODO/FIXME и давность |
| `GET /repositories/{id}/documentation?runId` | `ISourceCraftDocumentationSource` | git: README/LICENSE/CONTRIBUTING/CODEOWNERS/инструкции |
| `GET /repositories/{id}/security/findings` | `ISourceCraftSecuritySource` | всегда `Unavailable` (AppSec нет в публичном API) |
| `PUT /repositories/{id}/snapshots/{runId}` | — | клон → S3; тело `{"withBlobs":false,"depth":0}` необязательно |
| `GET /repositories/{id}/snapshots/{runId}` | — | метаданные снапшота |
| `DELETE /repositories/{id}/snapshots/{runId}` | — | удаление снапшота |
| `POST /internal/snapshots/reap` | — | очистка просроченных снапшотов; заголовок `X-Internal-Token` |

Служебные маршруты serverless-точки входа (`cmd/function`):

| Метод и путь | Назначение | Защита |
|---|---|---|
| `POST /internal/queue/messages` | триггер Message Queue: `CreateSnapshot` по каждому сообщению | `X-Internal-Token` |
| `POST /internal/snapshots/reap` | таймер-триггер: очистка просроченных снапшотов | `X-Internal-Token` |

### Конверт ответа

Каждый ответ Go приходит в едином конверте:

```json
{
  "requestId": "…",
  "collectedAt": "2026-09-25T15:00:00Z",
  "status": "Available",
  "data": { },
  "reason": "…",
  "nextPageToken": "…"
}
```

- `status`: `Available`, `NoData` (пустой список, нет коммитов) или `Unavailable` (источник упал или репозиторий превысил лимит).
- `reason` и `nextPageToken` опускаются (`omitempty`), если пусты.
- `requestId` дублируется в заголовке `X-Request-Id`.
- Сбой одного источника отвечает HTTP `200` со статусом `Unavailable` и **не роняет** остальные категории.
- Имена полей — camelCase, enum'ы — строками; на стороне C# используется `JsonStringEnumConverter`.

C# разбирает конверт в `SourceCraftEnvelope<T>` и преобразует в `SourceCraftPage<T>` → `SourceCraftResult<T>` (статус, данные, причина). `nextPageToken` используется для пагинации каталога (`SourceCraftRepositoryCatalogAdapter`).

### Ошибки

Ошибка возвращается как `{"requestId":…,"code":…,"message":…}`. HTTP-коды:

| Код | Когда |
|---|---|
| `400` | некорректный запрос (битый JSON, неверный `state`/`depth`, `invalid_grant` при входе) |
| `401` | нет/не принят токен доступа |
| `403` | нет прав на репозиторий либо нет `X-Internal-Token` |
| `404` | репозиторий или снапшот не найден |
| `409` | повторная операция (по ТЗ) |
| `501` | Я ID OAuth не сконфигурирован (`/auth/url`, `/auth/token` без кредов) |
| `502` | внешний источник вернул ошибку; не обработано хотя бы одно сообщение очереди |
| `503` | внешний сервис временно недоступен (например, Я ID) |
| `504` | таймаут сбора данных |

Маппинг ошибок Go — `httpapi.Server.respond`. На стороне C# неуспешный HTTP-статус превращается в `SourceCraftResult` со статусом `Unavailable` и причиной (`SourceCraftHttpClient.SendAsync`).

### Модели (кратко)

Модели JSON повторены в `Application/SourceCraft/Models` (C#) и `internal/contract` (Go):

- `SourceCraftRepository` — id, name, fullName, url, language, likesCount, lastActivityAt, isPrivate, defaultBranch.
- `CommitActivity` — totalCount, firstCommitAt, lastCommitAt, commitsByDay (`"yyyy-MM-dd"` → count).
- `Contributor` — login, commitsCount, isBot.
- `ReleaseInfo` — name, tag, publishedAt.
- `IssueInfo` / `MergeRequestInfo` — состояние, даты, `firstResponseAt`, review-комментарии.
- `PipelineRun` — id, status, branch, startedAt, finishedAt.
- `CodeHealthReport` — todoCount, fixmeCount, totalCommentCount, oldestCommentAge (TimeSpan в формате `"c"`).
- `DocumentationReport` — флаги README/LICENSE/CONTRIBUTING/CODEOWNERS/инструкций.
- `SecurityFinding` — kind (Sast/Sca/SecretScanning), severity, status, title, package, filePath.
- `SourceCraftUser` — id, login, displayName, email (обычно `null`).
