# Методика Repo Health Score

Расчёт живёт в `Application/HealthCheck` и полностью настраивается через `IOptions<HealthCheckOptions>` (значения — в `Presenter/appsettings.json`), без хардкода чисел. Версия методики: **1.0** (`HealthCheckOptions.MethodologyVersion`).

Расчёт воспроизводим и объясним: каждая категория собирается из метрик, метрики сходятся с баллом категории, итог сходится с баллами категорий.

## Общий Score (0–100)

Итоговый Repo Health Score — **взвешенное среднее оценок категорий**, при этом учитываются только категории со статусом `Available`:

```
Score = round( Σ (Score_i · Weight_i) / Σ Weight_i ),  i ∈ Available
```

- Если доступных категорий нет — Score равен `ScoreScale.MinimumScore` (0).
- Категории со статусом `NoData` («Нет данных») **исключаются**, их веса не участвуют. Отсутствие данных не ухудшает Score (веса доступных норммируются самим делением на `Σ Weight_i`).
- Категории со статусом `Unavailable` также не считаются `Available` и в расчёт не входят.
- Результат округляется (`MidpointRounding.AwayFromZero`) и зажимается в границы шкалы `ScoreScale` (`MinimumScore = 0`, `MaximumScore = 100`).

Код: `HealthScoreCalculator.Calculate`.

## Веса категорий

`CategoryWeightsOptions` (сумма = 1.0):

| Категория (`ScoreCategory`) | Вес |
|---|---|
| `Security` | 0.20 |
| `CodeHealth` | 0.20 |
| `Activity` | 0.15 |
| `Documentation` | 0.15 |
| `CiCd` | 0.15 |
| `Issues` | 0.15 |

Веса можно менять, но каждое изменение нужно обосновывать и компенсировать другой метрикой. Вес категории хранится в `CategoryScoreResult.Weight` и попадает в объяснение.

## Оценка категории

Оценка категории — **взвешенное среднее нормализованных метрик** (`MetricScore.NormalizedScore`):

```
CategoryScore = round( Σ (NormalizedScore_m · Weight_m) / Σ Weight_m ),  m ∈ Available
```

- В расчёт входят только метрики со статусом `Available`.
- Если доступных метрик нет — категория получает статус `NoData`.
- Если суммарный вес доступных метрик равен нулю (например, остались только информационные метрики), берётся простое среднее `NormalizedScore`.
- Балл округляется до целого.

Код: `CategoryScoreCalculator.FromMetrics`.

## Нормализация метрики

`MetricNormalizer.Normalize(value, worstValue, bestValue)` — линейная нормировка от «худшего» к «лучшему» значению в границах шкалы `ScoreScale`:

```
ratio = (value - worstValue) / (bestValue - worstValue)
NormalizedScore = clamp(ratio · (MaximumScore - MinimumScore) + MinimumScore)
```

- Если `worstValue == bestValue`: `NormalizedScore = MaximumScore` при `value == bestValue`, иначе `MinimumScore`.
- Результат всегда зажат в `[MinimumScore, MaximumScore]`.
- Для штрафных метрик (Security, Code health) «лучшему» значению соответствует отсутствие находок, поэтому `NormalizedScore = clamp(MaximumScore − count · penalty)`.

Код: `MetricNormalizer.Normalize`.

## Метрики по категориям

### Security (вес 0.20)

Категория доступна только при `SecurityAvailability == Available`; иначе — `NoData`. Считаются **открытые** находки AppSec (`SecurityFindingStatus.Open`) по критичности; исправленные находки — информационная метрика с весом 0. Вес метрики равен соответствующему штрафу:

| Метрика (`MetricCode`) | Формула | Вес |
|---|---|---|
| `SecurityCriticalFindings` | `clamp(100 − Critical · 25)` | 25 |
| `SecurityHighFindings` | `clamp(100 − High · 15)` | 15 |
| `SecurityMediumFindings` | `clamp(100 − Medium · 8)` | 8 |
| `SecurityLowFindings` | `clamp(100 − Low · 3)` | 3 |
| `SecurityFixedFindings` | `clamp(0 + Fixed · 3)` | 0 (информационная) |

Пороги штрафов: `SecurityScoringOptions` (`CriticalPenalty = 25`, `HighPenalty = 15`, `MediumPenalty = 8`, `LowPenalty = 3`, `FixedFindingCredit = 3`).

Если открытых находок нет, все штрафные метрики дают 100 — категория 100. Если AppSec недоступен, ядро не штрафует, а добавляет рекомендацию-пометку «Подключите AppSec SourceCraft» (см. ниже).

### Code health (вес 0.20)

Категория доступна при `CodeHealthAvailability == Available`:

| Метрика (`MetricCode`) | RawValue | Формула | Вес |
|---|---|---|---|
| `CodeHealthTodo` | число TODO | `clamp(100 − TODO · 1)` | 1 |
| `CodeHealthFixme` | число FIXME | `clamp(100 − FIXME · 2)` | 2 |
| `CodeHealthStaleComments` | возраст самого старого комментария (дней) | `95`, если возраст > `StaleCommentAgeDays` (180), иначе `100` | 5 |

`CodeHealthScoringOptions`: `TodoPenalty = 1`, `FixmePenalty = 2`, `StaleCommentAgeDays = 180`, `StaleCommentPenalty = 5`.

### Активность (вес 0.15)

Категория доступна при `ActivityAvailability == Available`. Метрики равного веса 1:

| Метрика (`MetricCode`) | RawValue | Нормировка (worst → best) |
|---|---|---|
| `ActivityLastActivity` | дней с последней активности | `StaleAfterDays` (365) → `ActiveWithinDays` (30) |
| `ActivityCommitFrequency` | коммитов за окно `ActiveWithinDays` | 0 → `CommitFrequencyForFullScore` (30) |
| `ActivityContributors` | участников (без ботов) | 0 → `ContributorsForFullScore` (5) |
| `ActivityReleases` | релизов | 0 → `ReleasesForFullScore` (4) |
| `ActivityMergeRequests` | merge request'ов | 0 → `MergeRequestsForFullScore` (4) |

Последняя активность берётся как `Commits.LastCommitAt`, с откатом на `Repository.LastActivityAt`. Метрика MR добавляется только при `CollaborationAvailability == Available`.

`ActivityScoringOptions`: `ActiveWithinDays = 30`, `StaleAfterDays = 365`, `CommitFrequencyForFullScore = 30`, `ContributorsForFullScore = 5`, `ReleasesForFullScore = 4`, `MergeRequestsForFullScore = 4`.

### Документация и лучшие практики (вес 0.15)

Категория доступна при `DocumentationAvailability == Available`. Бинарные метрики: наличие → 100, отсутствие → 0. Веса внутри категории (`DocumentationScoringOptions`):

| Метрика (`MetricCode`) | Проверка | Вес |
|---|---|---|
| `DocumentationReadme` | README | 0.30 |
| `DocumentationLicense` | LICENSE/LICENCE/COPYING | 0.20 |
| `DocumentationContributing` | CONTRIBUTING | 0.10 |
| `DocumentationCodeOwners` | CODEOWNERS | 0.10 |
| `DocumentationLocalRun` | инструкция локального запуска | 0.15 |
| `DocumentationBuildAndTest` | инструкция сборки и тестов | 0.15 |

### CI/CD (вес 0.15)

Категория доступна при `PipelineAvailability == Available`. Если запусков нет — категория `Available` с баллом 0 и единственной метрикой `CiCdPresence = 0`.

| Метрика (`MetricCode`) | RawValue | Нормировка (worst → best) | Вес |
|---|---|---|---|
| `CiCdPresence` | число запусков | 0 → `PipelineRunsForFullScore` (10) | 1 |
| `CiCdSuccessRatio` | доля успешных среди завершённых (`Success`/`Failed`) | 0 → `MinimumSuccessRatio` (0.80) | 1 |
| `CiCdPipelineDuration` | средняя длительность завершённых, мин | `MaxPipelineDurationMinutes` (30) → 0 | 1 |

`CiCdScoringOptions`: `PipelineRunsForFullScore = 10`, `MinimumSuccessRatio = 0.80`, `MaxPipelineDurationMinutes = 30`. Если ни у одного запуска нет времени завершения, длительность не штрафуется (`NormalizedScore = 100`).

### Issues (вес 0.15)

Категория доступна при `CollaborationAvailability == Available`. Метрики равного веса 1:

| Метрика (`MetricCode`) | RawValue | Нормировка (worst → best) |
|---|---|---|
| `IssuesOpen` | открытых issue | `MaxOpenIssues` (50) → 0 |
| `IssuesClosed` | закрытых issue | доля закрытых 0 → 1 |
| `IssuesStale` | зависших открытых | доля зависших 1 → 0 |
| `IssuesFirstResponse` | среднее время первого ответа, дней | `MaxFirstResponseDays` (7) → 0 |
| `IssuesCloseTime` | среднее время закрытия, дней | `MaxCloseDays` (30) → 0 |

Зависшей считается открытая issue, у которой `(now − (UpdatedAt ?? CreatedAt))` больше `StaleIssueAgeDays` (30). Метрики первого ответа и времени закрытия получают статус `NoData` (и исключаются из среднего), если подходящих issue нет.

`IssuesScoringOptions`: `StaleIssueAgeDays = 30`, `MaxFirstResponseDays = 7`, `MaxCloseDays = 30`, `MaxOpenIssues = 50`.

## «Нет данных» как отдельный статус

`DataStatus` (`Domain/Enums`) имеет три значения:

- `Available` — данные получены; категория/метрика участвует в расчёте.
- `NoData` — данных нет (пустой источник, отсутствие сущностей). Это **не** плохой результат: метрика/категория исключается из среднего, отсутствие данных не штрафует Score.
- `Unavailable` — источник недоступен (сбой, таймаут, превышен лимит, нет доступа). Тоже не попадает в `Available`.

Категория становится `NoData`, если её основной источник недоступен (`ActivityAvailability`, `CollaborationAvailability`, `PipelineAvailability`, `CodeHealthAvailability`, `DocumentationAvailability`, `SecurityAvailability` не равны `Available`) или если доступных метрик не осталось. В API/отчёте «Нет данных» отображается текстом **«Нет данных»**, а `Unavailable` — «Источник недоступен».

Особый случай: если **Security** недоступен, ядро добавляет рекомендацию-пометку «Подключите AppSec SourceCraft» с приоритетом `Low` и пояснением, что статус «Нет данных» не штрафует итоговый Score (`RecommendationGenerator.SecurityDataNotice`).

## Рекомендации

Рекомендации строятся на фактах из `MetricScore.RawValue` и имеют приоритет и `SourceReference`. Код: `RecommendationGenerator.Generate`.

Алгоритм:

1. Для Security с любым статусом, кроме `Available`, добавляется пометка о подключении AppSec и обработка категории завершается.
2. Категория пропускается, если она не `Available` или её балл ≥ `MinimumAcceptableScore` (70).
3. Иначе формируется рекомендация:
   - **Приоритет** (`PriorityFor`): `< 25` → `Critical`, `< 50` → `High`, иначе `Medium`.
   - **Проблема** (`Problem`) — факты из `RawValue`: например, «Открытые уязвимости AppSec: критических 2, высоких 1», «Технический долг: TODO — 47, FIXME — 3, самый старый комментарий — 200 дн.», «Отсутствует: лицензия, CONTRIBUTING», «CI/CD: запусков — 12, успешность — 67%, средняя длительность — 18 мин.».
   - **Действие** (`Action`) — по категории (например, «Сократите TODO/FIXME и старый технический долг»).
   - **Ожидаемый эффект** (`ExpectedImpact`) — фиксированная формулировка: доведение категории до 70 повысит итоговый Repo Health Score.
   - **Источник** (`SourceReference`) — список кодов метрик категории (если метрик нет — имя категории). По возможности указывается файл/pipeline/vulnerability/commit/issue/MR.
4. При чтении анализа рекомендации сортируются по приоритету **по убыванию** (`GetRepositoryAnalysisUseCase`).

Пороги рекомендаций (`RecommendationScoringOptions`): `MinimumAcceptableScore = 70`, `StrengthScore = 85`, `HighPriorityScore = 50`, `CriticalPriorityScore = 25`.

## Сильные и слабые стороны

- **Сильные стороны** — категории `Available` с баллом ≥ `StrengthScore` (85).
- **Слабые стороны** — категории `Available` с баллом < `MinimumAcceptableScore` (70).

Считаются и в ядре (`HealthCheckEngine`), и при чтении сохранённого анализа (`GetRepositoryAnalysisUseCase`).

## Дополнительно

- Значения по умолчанию — в `Presenter/appsettings.json`, секция `HealthCheckOptions`. Все числа — настройки, а не константы в коде.
- Расчёт устойчив к перекосам: популярность (лайки) не входит в Score, второстепенные метрики имеют ограниченный вес, а отсутствие данных не повышает и не понижает итог.
- Контрольные сценарии методики зафиксированы тестами: `Tests/SourceCraftRepoHealthChecker.UnitTests/HealthCheck/Scenarios/MethodologyControlScenariosTests.cs`.
