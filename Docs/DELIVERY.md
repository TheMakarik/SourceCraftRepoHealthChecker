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

Проект можно скачать с GitHub или SourceCraft и запустить из корня репозитория — стартовым скриптам
нужны `docker-compose.yml`, `Backend/` и `Frontend/`.

**Интерактивно (рекомендуется).** Скрипты [`Scripts/prepare.sh`](../Scripts/prepare.sh)
(Linux/macOS) и [`Scripts/prepare.bat`](../Scripts/prepare.bat) (Windows):

1. спрашивают нужные токены и объясняют, где их взять (Я ID ClientID/Secret — обязательно;
   сервисный SourceCraft PAT — опционально, для наполнения публичного рейтинга);
2. генерируют ключ шифрования `AI_TOKEN_ENCRYPTION_KEY`;
3. пишут `.env` в корень репозитория;
4. запускают проект (`Scripts/start.sh` / `Scripts/start.bat`).

```bash
bash Scripts/prepare.sh     # Linux/macOS
Scripts\prepare.bat         # Windows
```

**Запуск через Docker Compose.** Скрипты [`Scripts/start.sh`](../Scripts/start.sh)
(Linux/macOS) и [`Scripts/start.bat`](../Scripts/start.bat) (Windows) при отсутствии Docker
устанавливают его, при отсутствии `.env` создают его из `.env.example` (включая генерацию
`AI_TOKEN_ENCRYPTION_KEY`) и поднимают **PostgreSQL + backend + frontend**:

```bash
bash Scripts/start.sh       # Linux/macOS
Scripts\start.bat           # Windows
```

Эквивалент вручную:

```bash
cp .env.example .env        # заполните SOURCECRAFT_PAT и YANDEX_CLIENT_ID/SECRET
docker compose up --build -d
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

Отчёты выгружаются по репозиторию; для публичного репозитория доступны без входа. Ответ отдаётся
как загрузка файла — заголовок `Content-Disposition: attachment` с именем файла вида
`<repositoryId>.<ext>`. Шаблоны эндпоинтов (полный контракт — [`API.md`](API.md)):

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
  рекомендации); 3) вход через Я ID и анализ своего репозитория; 4) выгрузка отчёта Markdown/PDF;
  5) публичный бейдж Score из README (`/api/public/repositories/<id>/badge.svg`); 6) AI-резюме или
  AI-разбор с потоковым выводом (SSE).

## 8. Функции со звёздочкой (bonus)

Опциональные направления ТЗ реализованы и не подменяют обязательную часть (подробности — в
[`ARCHITECTURE.md`](ARCHITECTURE.md), §10, и [`API.md`](API.md)):

- **Публичный Score API:** `GET /api/public/repositories/{id}/score` — текущий балл и версия методики без входа.
- **README-badge:** `GET /api/public/repositories/{id}/badge.svg` — SVG quality badge с текущим Score.
- **Сравнение проектов:** `GET /api/repositories/compare?ids=<a>,<b>[,<c>,<d>]` — 2–4 репозитория по Score, категориям и метрикам.
- **Индекс достоверности:** `GET /api/repositories/{id}/integrity` — защита публичного рейтинга от накрутки.
- **Bus factor / карта владельцев:** `GET /api/repositories/{id}/ownership` — владельцы и bus factor по git-истории.
- **Review-разбор:** `GET /api/repositories/{id}/review-insights` — активность ревью по merge requests.
- **AI-summary и AI-разборы:** `POST /api/repositories/{id}/ai-summary`,
  `POST /api/repositories/{id}/ai-insights/{kind}` (`recommendations`, `explanation`, `action-plan`,
  `security-triage`, `risk-forecast`) с потоковым выводом SSE (`/stream`).
