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
| `GET /api/repositories/{id}/history` | История Score (точки `{ analyzedAt, score }` для графика динамики) |
| `GET /auth/login` | Начать вход через Я ID (state-cookie + редирект) |
| `GET /auth/callback?code=&state=` | Callback Я ID → подписанный тикет-куки |
| `GET /api/me` | Текущий пользователь |
| `GET /api/me/repositories` | Доступные пользователю репозитории (PAT из `Authorization` или сохранённый) |
| `POST /api/me/repositories/{id}/analyze` | Запустить анализ своего репозитория |
| `PUT /api/me/ai` | Сохранить настройки ИИ (провайдер, модель, baseUrl, токен — шифрованно) |
| `GET /api/me/ai` | Текущие настройки ИИ (без токена): 204, если не настроено |
| `POST /api/repositories/{id}/ai-summary` | AI-summary через выбранного провайдера |
| `POST /api/repositories/{id}/ai-insights/{kind}` | AI-разбор: `recommendations`, `explanation`, `action-plan`, `security-triage`, `risk-forecast` |

## Источники данных (внутри сервиса)

Отдельного микросервиса нет: сбор данных выполняют адаптеры `infrastructure` за портами `Application`.

| Источник | Где | Данные |
|---|---|---|
| SourceCraft REST (`https://api.sourcecraft.tech`, Refit) | `infrastructure/SourceCraft/ISourceCraftApi.cs` | каталог, репозиторий, issues, MR, релизы, CI-запуски, `/user`, `/me/repos` |
| AppSec SourceCraft (`https://appsec.sourcecraft.tech`, Refit) | `infrastructure/SourceCraft/SourceCraftSecurityAdapter.cs` | находки SAST/SCA/secrets (`GET /v1/defect-groups`) |
| git (LibGit2Sharp) | `infrastructure/SourceCraft/LocalGitRepositoryReader.cs` | коммиты, контрибьюторы, TODO/FIXME, документация, структура |
| Yandex ID OAuth (`https://oauth.yandex.ru`) | `infrastructure/SourceCraft/YandexIdClient.cs` | вход и профиль пользователя |
| Конфигурация `.env` | `infrastructure/Configuration/DotEnvEnvironmentFileLoader.cs` (за портом `IEnvironmentFileLoader`) | значения настроек |
