# API

## Технические проверки и страницы

| Метод и путь | Назначение |
|---|---|
| `GET /healthz` | Проверка живости (liveness): `{ status: "ok" }` |
| `GET /readyz` | Проверка готовности (readiness): доступность PostgreSQL. `{ status: "ready" }`; при недоступной БД — `503 { status: "degraded", reason: "database_unavailable" }` |
| `GET /rating` | Серверная HTML-страница рейтинга: `language` (один язык), `sort` (score/likes/activity), `page` (размер страницы 20) |
| `GET /repositories/{id}` | HTML-страница анализа репозитория (приватные — только владельцу) |
| `GET /ws/analysis` | WebSocket-канал push-статусов анализа (см. ниже) |

## Каталог и анализ

| Метод и путь | Назначение |
|---|---|
| `GET /api/repositories` | Рейтинг (JSON). Query: `language` (повторяемый), `sort` (score\|likes\|activity, по умолчанию score), `page` (с 1, по умолчанию 1), `pageSize` (по умолчанию 20), `hasCi` (bool), `minScore`, `maxScore`. Элементы содержат `placeDelta` — изменение места относительно предыдущего снимка |
| `POST /api/repositories/refresh` | Обновить каталог открытых репозиториев. Требует авторизации либо заголовка `X-Internal-Token`; ответ `{ refreshed }` |
| `GET /api/repositories/languages` | Присутствующие языки для фильтра рейтинга |
| `GET /api/repositories/{id}/analysis` | Анализ: Score, категории, сильные/слабые, метрики (объяснение), рекомендации, дата; приватные — владельцу |
| `GET /api/repositories/{id}/history` | История Score (точки `{ analyzedAt, score }` для графика динамики) |
| `GET /api/repositories/{id}/structure` | Health Check по папкам (структура) |
| `GET /api/repositories/{id}/tree?path=&recursive=` | Дерево файлов; `path` по умолчанию пустой, `recursive` по умолчанию `false` |
| `GET /api/repositories/{id}/file?path=` | Содержимое файла; `path` обязателен |
| `GET /api/repositories/{id}/folders` | Анализ по папкам (файлы, TODO/FIXME, README/LICENSE/tests) |
| `GET /api/repositories/{id}/review-insights` | Метрики review/PR (скорость, объём обсуждений) |
| `GET /api/repositories/{id}/integrity` | Индекс достоверности (защита рейтинга от накрутки) |
| `GET /api/repositories/{id}/ownership` | Владельцы и bus factor по git-истории |
| `GET /api/repositories/compare?ids=a,b[,c,d]` | Сравнение 2–4 репозиториев (id через запятую, дубликаты отбрасываются) по Score, категориям и метрикам |
| `GET /api/repositories/{id}/report?format=markdown\|json\|html\|pdf` | Выгрузка отчёта как attachment (по умолчанию markdown) |
| `GET /api/repositories/{id}/report.md` | Отчёт Markdown (совместимость) |

## Аутентификация и профиль (Я ID)

| Метод и путь | Назначение |
|---|---|
| `GET /auth/login` | Начать вход через Я ID (state-cookie + редирект) |
| `GET /auth/callback?code=&state=` | Callback Я ID → подписанный тикет-куки; при заданном `Authentication:FrontendRedirectUrl` — редирект, иначе JSON пользователя |
| `POST /auth/logout` | Удалить тикет- и state-куки. Ответ `204` |
| `GET /api/me` | Текущий пользователь |
| `GET /api/me/tokens` | Статус сохранённых токенов (без значений) |
| `POST /api/me/sourcecraft-token` | Сохранить PAT SourceCraft; тело `{ token }`; `204` или `404` |
| `GET /api/me/repositories` | Доступные пользователю репозитории (PAT из `Authorization` или сохранённый) |
| `POST /api/me/repositories/{id}/analyze` | Запустить анализ своего репозитория |

## ИИ (AI)

| Метод и путь | Назначение |
|---|---|
| `GET /api/me/ai` | Текущие настройки ИИ (без токена): `204`, если не настроено |
| `PUT /api/me/ai` | Сохранить настройки: `{ provider, baseUrl?, model, token? }` (токен шифрованно; без токена сохраняются только провайдер/модель). Ответ `204` |
| `GET /api/me/ai/models` | Курируемый список моделей по провайдерам: `[{ provider, models }]` (публичный) |
| `GET /api/me/ai/tokens` | Статус сохранённых AI-токенов (без значений) |
| `POST /api/me/ai/test` | Тестовый запрос к сохранённым провайдеру/модели/токену: `{ ok, message }` |
| `POST /api/repositories/{id}/ai-summary` | AI-summary через выбранного провайдера |
| `POST /api/repositories/{id}/ai-insights/{kind}` | AI-разбор: `recommendations`, `explanation`, `action-plan`, `security-triage`, `risk-forecast` |
| `POST /api/repositories/{id}/ai-summary/stream` | SSE-поток AI-summary (`text/event-stream`) |
| `POST /api/repositories/{id}/ai-insights/{kind}/stream` | SSE-поток AI-разбора по `kind` (`text/event-stream`) |

SSE-события: строки `data: { "type": ..., "text": ... }`; `type` — `delta`, `thinking`, `done` или `error`.

## WebSocket

| Метод и путь | Назначение |
|---|---|
| `GET /ws/analysis` | WebSocket (не HTTP-запрос — иначе `400 { error: "websocket_required" }`). При подключении отправляет `{ type: "snapshot", items: [...] }`, далее `{ type: "status", repositoryId, status, score, updatedAt }`; статусы: `running`, `completed`, `failed`. Отдаются только репозитории, доступные пользователю |

## Публичный API и функции со звёздочкой (bonus)

| Метод и путь | Назначение |
|---|---|
| `GET /api/public/repositories/{id}/score` | Публичный Score с версией методики (без входа) |
| `GET /api/public/repositories/{id}/badge.svg` | SVG quality badge с текущим Score (`image/svg+xml`, публичный, `Cache-Control: public, max-age=300, must-revalidate`) |

## Источники данных (внутри сервиса)

Отдельного микросервиса нет: сбор данных выполняют адаптеры `infrastructure` за портами `Application`.

| Источник | Где | Данные |
|---|---|---|
| SourceCraft REST (`https://api.sourcecraft.tech`, Refit) | `infrastructure/SourceCraft/ISourceCraftApi.cs` | каталог, репозиторий, issues, MR, релизы, CI-запуски, `/user`, `/me/repos`, комментарии, дерево файлов |
| AppSec SourceCraft (`https://appsec.sourcecraft.tech`, Refit) | `infrastructure/SourceCraft/SourceCraftSecurityAdapter.cs` (порт `IAppSecApi`) | находки SAST/SCA/secrets (`GET /v1/defect-groups`, severity/engine/status) |
| git (LibGit2Sharp) | `infrastructure/SourceCraft/LocalGitRepositoryReader.cs` | коммиты, контрибьюторы, TODO/FIXME, документация, структура |
| Yandex ID OAuth (`https://oauth.yandex.ru`) | `infrastructure/SourceCraft/YandexIdClient.cs` (за `IYandexIdClient`) | вход и профиль пользователя |
| Конфигурация `.env` | `infrastructure/Configuration/DotEnvEnvironmentFileLoader.cs` (за портом `IEnvironmentFileLoader`) | значения настроек |
