# Сдача проекта — чек-лист (TZ 12.2)

Итоговый комплект материалов для сдачи SourceCraftRepoHealthChecker. Плейсхолдеры `TODO:`
оставлены только для **внешних** артефактов (адрес демо-стенда, слайды, видео) — их заполняют
после публикации.

## 1. Ссылка на репозиторий в SourceCraft

- Репозиторий: https://git.sourcecraft.dev/themakarik/repo-health-checker
- Зеркало на GitHub: https://github.com/TheMakarik/SourceCraftRepoHealthChecker
- README с описанием, стеком и запуском: [`README.md`](../README.md).
- Лицензия: [`LICENSE.txt`](../LICENSE.txt); правила поведения: [`CODE_OF_CONDUCT.md`](../CODE_OF_CONDUCT.md);
  владельцы кода: [`CODEOWNERS`](../CODEOWNERS).

## 2. URL демонстрационного стенда

- Демо-стенд: `TODO: <URL стенда>` (внешний ресурс, публикуется после развёртывания)
- Публичный рейтинг: `TODO: <URL>/`
- Пример страницы анализа: `TODO: <URL>/repositories/<sourceCraftId>`
- Методика: `TODO: <URL>/methodology`

> Локальный запуск (порты фиксированы): Frontend `http://localhost:8080`,
> Backend API `http://localhost:5172`. Анализ репозитория доступен по
> `http://localhost:8080/repositories/<sourceCraftId>`, методика — по `http://localhost:8080/methodology`.

## 3. Инструкция запуска

Скрипты — [`Scripts/start.sh`](../Scripts/start.sh) (Linux/macOS) и
[`Scripts/start.bat`](../Scripts/start.bat) (Windows): создают `.env` из `.env.example` (включая
генерацию `AI_TOKEN_ENCRYPTION_KEY`), собирают и поднимают PostgreSQL + бэкенд + фронтенд через
Docker Compose. Интерактивная подготовка секретов — [`prepare.sh`](../prepare.sh) /
[`prepare.bat`](../prepare.bat).

```bash
cp .env.example .env      # заполните SOURCECRAFT_PAT и YANDEX_CLIENT_ID/SECRET
./Scripts/start.sh        # или Scripts\start.bat в Windows
```

- Frontend: http://localhost:8080
- Backend API: http://localhost:5172
- Сервисы: [`docker-compose.yml`](../docker-compose.yml) (PostgreSQL + backend + frontend). MinIO в
  Compose нет; S3-совместимое хранилище DataProtection — опционально (см. `Docs/ARCHITECTURE.md`).

Подробности сборки/тестов — [`ARCHITECTURE.md`](ARCHITECTURE.md), контракт — [`API.md`](API.md).

## 4. Сопроводительная документация

- Архитектура и методика: [`ARCHITECTURE.md`](ARCHITECTURE.md)
- Контракт API: [`API.md`](API.md)
- Крупный репозиторий (сквозной прогон TZ 9.2/11.1): [`LARGE_REPO.md`](LARGE_REPO.md)
- Правила разработки и структура: [`AGENTS.md`](../AGENTS.md)

## 5. Примеры отчётов

Отчёты выгружаются по репозиторию; для публичного репозитория доступны без входа. Шаблоны
эндпоинтов (полный контракт — [`API.md`](API.md)):

| Формат | Эндпоинт |
|---|---|
| Markdown | `GET /api/repositories/<id>/report?format=markdown` (совместимость: `/report.md`) |
| JSON | `GET /api/repositories/<id>/report?format=json` |
| HTML | `GET /api/repositories/<id>/report?format=html` |
| PDF | `GET /api/repositories/<id>/report?format=pdf` |

Локальные примеры (замените `<id>` на `sourceCraftId`):

- Markdown: `http://localhost:5172/api/repositories/<id>/report.md`
- PDF: `http://localhost:5172/api/repositories/<id>/report?format=pdf`

После публикации стенда те же пути доступны на внешнем URL:

- Markdown: `TODO: <URL>/api/repositories/<id>/report.md`
- PDF: `TODO: <URL>/api/repositories/<id>/report?format=pdf`

## 6. Обоснование пользы

Сервис даёт объективную объяснимую оценку «здоровья» репозитория (Repo Health Score 0–100) по
шести категориям, опираясь на реальные факты SourceCraft и находки AppSec. Команда/мейнтейнер
видит не абстрактный балл, а сильные и слабые стороны, приоритизированные рекомендации с
подтверждающими фактами и ссылками, динамику Score во времени и выгрузку отчёта. Публичный
рейтинг с защитой от накрутки (индекс достоверности) помогает сравнивать проекты по здоровью, а не
только по популярности. Периодический пересчёт по расписанию поддерживает актуальность оценок.

## 7. Презентация и видео (внешние артефакты)

- Презентация: `TODO: ссылка на слайды`
- Демонстрационное видео: `TODO: ссылка на видео`
- Сценарий демо: 1) рейтинг и фильтр по языкам; 2) страница анализа (Score, категории,
  рекомендации); 3) вход через Я ID и анализ своего репозитория; 4) выгрузка отчёта Markdown/PDF.
