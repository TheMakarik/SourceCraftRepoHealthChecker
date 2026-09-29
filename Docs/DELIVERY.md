# Сдача проекта — чек-лист (TZ 12.2)

Итоговый комплект материалов для сдачи SourceCraftRepoHealthChecker. Заполните плейсхолдеры
`TODO:` перед отправкой.

## 1. Ссылка на репозиторий в SourceCraft

- Репозиторий: https://git.sourcecraft.dev/themakarik/repo-health-checker
- README с описанием, стеком и запуском: `README.md` (корень репозитория).
- Лицензия и вклад: см. репозиторий.

## 2. URL демонстрационного стенда

- Демо-стенд: `TODO: <URL стенда>`
- Публичный рейтинг: `TODO: <URL>/`
- Пример страницы анализа: `TODO: <URL>/repositories/<sourceCraftId>`
- Методика: `TODO: <URL>/methodology`

> Если стенд поднимается локально, использовать порты ниже: Frontend `http://localhost:8080`,
> Backend API `http://localhost:5172`.

## 3. Инструкция запуска

Скрипты — `Scripts/start.sh` (Linux/macOS) и `Scripts/start.bat` (Windows): создают `.env` из
`.env.example` (включая генерацию `AI_TOKEN_ENCRYPTION_KEY`), собирают и поднимают
PostgreSQL + бэкенд + фронтенд через Docker Compose.

```bash
cp .env.example .env      # заполните SOURCECRAFT_PAT и YANDEX_CLIENT_ID/SECRET
./Scripts/start.sh        # или Scripts\start.bat в Windows
```

- Frontend: http://localhost:8080
- Backend API: http://localhost:5172
- Сервисы: `docker-compose.yml` (PostgreSQL + backend + frontend). MinIO в Compose нет;
  S3-совместимое хранилище DataProtection — опционально (см. `Docs/ARCHITECTURE.md`).

Подробности сборки/тестов — `Docs/ARCHITECTURE.md`, контракт — `Docs/API.md`.

## 4. Сопроводительная документация

- Архитектура и методика: [`ARCHITECTURE.md`](ARCHITECTURE.md)
- Контракт API: [`API.md`](API.md)
- Правила разработки и структура: `AGENTS.md`

## 5. Примеры отчётов

Отчёт по репозиторию (открытый репозиторий доступен всем): замените `<id>` на `sourceCraftId`.

| Формат | Эндпоинт |
|---|---|
| Markdown | `GET /api/repositories/<id>/report?format=markdown` (совместимость: `/report.md`) |
| JSON | `GET /api/repositories/<id>/report?format=json` |
| HTML | `GET /api/repositories/<id>/report?format=html` |
| PDF | `GET /api/repositories/<id>/report?format=pdf` |

Примеры (заполнить после публикации стенда):

- Markdown: `TODO: <URL>/api/repositories/<id>/report.md`
- PDF: `TODO: <URL>/api/repositories/<id>/report?format=pdf`

## 6. Обоснование пользы

Сервис даёт объективную объяснимую оценку «здоровья» репозитория (Repo Health Score 0–100) по
шести категориям, опираясь на реальные факты SourceCraft и находки AppSec. Команда/мейнтейнер
видит не абстрактный балл, а сильные и слабые стороны, приоритизированные рекомендации с
подтверждающими фактами и ссылками, динамику Score во времени и выгрузку отчёта. Публичный
рейтинг с защитой от накрутки (индекс достоверности) помогает сравнивать проекты по здоровью, а не
только по популярности. Периодический пересчёт по расписанию поддерживает актуальность оценок.

## 7. Презентация и видео

- Презентация: `TODO: ссылка на слайды`
- Демонстрационное видео: `TODO: ссылка на видео`
- Сценарий демо: `TODO: 1) рейтинг и фильтр; 2) страница анализа; 3) вход через Я ID и анализ
  своего репозитория; 4) выгрузка отчёта Markdown/PDF`
