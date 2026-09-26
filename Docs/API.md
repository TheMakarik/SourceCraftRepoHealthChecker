# API

## Presenter (C#)

| Метод и путь | Назначение |
|---|---|
| `GET /healthz` | Проверка живости |
| `GET /rating` | Серверная HTML-страница рейтинга (фильтр по языку, сортировка) |
| `GET /repositories/{id}` | HTML-страница анализа репозитория |
| `GET /api/repositories` | Рейтинг (JSON): `language`, `sort` (score/likes/activity), `page`, `pageSize` |
| `POST /api/repositories/refresh` | Обновить каталог открытых репозиториев |
| `GET /api/repositories/{id}/analysis` | Анализ: Score, категории, сильные/слабые, метрики (объяснение), рекомендации, дата; приватные — владельцу |
| `GET /api/repositories/{id}/report?format=markdown\|json\|html\|pdf` | Выгрузка отчёта (по умолчанию markdown) |
| `GET /api/repositories/{id}/report.md` | Отчёт Markdown (совместимость) |
| `GET /api/repositories/{id}/structure` | Health Check по папкам (структура) |
| `GET /auth/login` | Начать вход через Я ID (state-cookie + редирект) |
| `GET /auth/callback?code=&state=` | Callback Я ID → подписанный тикет-куки |
| `GET /api/me` | Текущий пользователь |
| `GET /api/me/repositories` | Доступные пользователю репозитории (PAT из `Authorization` или сохранённый) |
| `POST /api/me/repositories/{id}/analyze` | Запустить анализ своего репозитория |
| `PUT /api/me/ai` | Сохранить настройки ИИ (провайдер, модель, baseUrl, токен — шифрованно) |
| `POST /api/repositories/{id}/ai-summary` | AI-summary через выбранного провайдера |

## Внутренний контракт Go-сервиса

Базовый хост — `SourceCraftServiceOptions:BaseUrl`. Конверт ответа:
`{ "requestId", "collectedAt", "status": "Available|NoData|Unavailable", "data", "reason", "nextPageToken" }`.

| Метод и путь | Источник |
|---|---|
| `POST /auth/url`, `POST /auth/token`, `GET /auth/me`, `GET /auth/repositories` | Я ID OAuth |
| `GET /repositories`, `GET /repositories/{id}` | API SourceCraft |
| `GET /repositories/{id}/activity/commits\|contributors\|releases` | git / API |
| `GET /repositories/{id}/issues`, `.../merge-requests` | API SourceCraft |
| `GET /repositories/{id}/pipelines` | API SourceCraft |
| `GET /repositories/{id}/security/findings` | AppSec API |
| `GET /repositories/{id}/code-health` | git (TODO/FIXME) |
| `GET /repositories/{id}/documentation` | git (файлы/инструкции) |
| `GET /repositories/{id}/structure` | git (структура папок) |
| `PUT\|GET\|DELETE /repositories/{id}/snapshots/{runId}` | S3-снапшоты (TTL) |
| `POST /internal/snapshots/reap` | Таймер-очистка (внутренний токен) |
| `POST /internal/queue/messages` | Message Queue-хендлер (внутренний токен) |
