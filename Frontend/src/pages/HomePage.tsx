import { useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  Badge,
  Button,
  Dropdown,
  Input,
  Option,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow
} from "@fluentui/react-components";
import { ArrowLeft24Regular, ArrowRight24Regular, ArrowSync24Regular, Play24Regular } from "@fluentui/react-icons";
import { useAnalyze, useLanguages, useLeaderboard, useRefresh, useRepositoryIntegrity } from "../shared/api/hooks";
import { formatDate, formatDateShort, languageDisplayName, scoreTone, toneColor } from "../shared/api/labels";
import { ApiError } from "../shared/api/client";
import { useAnalysisStatus } from "../shared/api/useAnalysisStatus";
import type { IntegrityStatus } from "../shared/api/types";
import { appConfig } from "../shared/config";
import { ErrorView, LoadingView } from "../shared/ui/Status";
import { AnalysisStatusBadge } from "../widgets/AnalysisStatusBadge";
import { LanguageTabs } from "../widgets/LanguageTabs";
import { RepoActionsButton, RepoContextMenu } from "../widgets/RepoContextMenu";

const integrityLabels: Record<IntegrityStatus, string> = {
  ok: "Чисто",
  check: "Проверяется",
  suspicious: "Подозрительно"
};

const integrityColors: Record<IntegrityStatus, "success" | "warning" | "danger"> = {
  ok: "success",
  check: "warning",
  suspicious: "danger"
};

function PlaceDelta({ delta }: { delta?: number | null }) {
  if (delta === null || delta === undefined)
    return null;
  if (delta === 0)
    return <span className="muted" title="Позиция не изменилась">—</span>;
  const rising = delta > 0;
  return (
    <span
      className={rising ? "tone-good" : "tone-bad"}
      title={rising ? `Поднялся на ${delta}` : `Опустился на ${Math.abs(delta)}`}
      style={{ whiteSpace: "nowrap" }}
    >
      {rising ? "▲" : "▼"}{Math.abs(delta)}
    </span>
  );
}

function IntegrityBadge({ sourceCraftId }: { sourceCraftId: string }) {
  const integrity = useRepositoryIntegrity(sourceCraftId);
  if (integrity.isPending)
    return <Spinner size="tiny" />;
  if (integrity.isError || !integrity.data)
    return <span className="muted">—</span>;

  const { status, signals } = integrity.data;
  return (
    <Badge
      appearance="tint"
      color={integrityColors[status]}
      title={signals.length > 0 ? signals.join("\n") : undefined}
    >
      {integrityLabels[status]}
    </Badge>
  );
}

function RepoStatus({
  sourceCraftId,
  score,
  analyzedAt
}: {
  sourceCraftId: string;
  score: number | null;
  analyzedAt?: string | null;
}) {
  const { statusFor } = useAnalysisStatus();
  const status = statusFor(sourceCraftId);
  if (status)
    return <AnalysisStatusBadge status={status} score={score} />;
  if (analyzedAt)
    return (
      <span className="muted" title={`Последний успешный анализ: ${formatDate(analyzedAt)}`}>
        Сохранён{score === null ? "" : ` · ${score}`}
      </span>
    );
  return <span className="muted">—</span>;
}

function parseScoreInput(value: string): number | undefined {
  const trimmed = value.trim();
  if (trimmed === "")
    return undefined;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : undefined;
}

export function HomePage() {
  const [selectedLanguages, setSelectedLanguages] = useState<string[]>([]);
  const [sort, setSort] = useState<string>("score");
  const [hasCi, setHasCi] = useState<"all" | "yes" | "no">("all");
  const [minScore, setMinScore] = useState("");
  const [maxScore, setMaxScore] = useState("");
  const [page, setPage] = useState(1);
  const query = useMemo(
    () => ({
      languages: selectedLanguages,
      sort,
      page,
      pageSize: appConfig.leaderboardPageSize,
      hasCi: hasCi === "all" ? undefined : hasCi === "yes",
      minScore: parseScoreInput(minScore),
      maxScore: parseScoreInput(maxScore)
    }),
    [selectedLanguages, sort, page, hasCi, minScore, maxScore]
  );
  const leaderboard = useLeaderboard(query);
  const languages = useLanguages();
  const refresh = useRefresh();
  const analyze = useAnalyze();
  const navigate = useNavigate();
  const [analyzeError, setAnalyzeError] = useState<string | null>(null);

  const runAnalyze = (sourceCraftId: string) => {
    setAnalyzeError(null);
    analyze.mutate(sourceCraftId, {
      onSuccess: () => navigate(`/repositories/${sourceCraftId}`),
      onError: (error) => {
        const status = error instanceof ApiError ? error.status : undefined;
        setAnalyzeError(
          status === 401
            ? "Нужен вход и PAT — настройте в личном кабинете."
            : status === 403
              ? "Нет доступа к этому репозиторию (нужен PAT владельца)."
              : "Не удалось запустить анализ. Попробуйте позже."
        );
      }
    });
  };

  const languageOptions = languages.data ?? [];
  const items = leaderboard.data?.items ?? [];
  const highestScore = items.reduce((max, item) => Math.max(max, item.score ?? 0), 0);
  const analyzed = items.filter((item) => item.analyzedAt).length;

  const handleLanguagesChange = (next: string[]) => {
    setSelectedLanguages(next);
    setPage(1);
  };

  const handleHasCiChange = (value: "all" | "yes" | "no") => {
    setHasCi(value);
    setPage(1);
  };

  const handleMinScoreChange = (value: string) => {
    setMinScore(value);
    setPage(1);
  };

  const handleMaxScoreChange = (value: string) => {
    setMaxScore(value);
    setPage(1);
  };

  const refreshErrorMessage = refresh.isError
    ? refresh.error instanceof ApiError
      ? refresh.error.status === 401
        ? "Нужен вход и PAT — настройте в личном кабинете."
        : `Не удалось обновить каталог (HTTP ${refresh.error.status}).`
      : "Не удалось обновить каталог. Проверьте соединение и попробуйте снова."
    : null;

  return (
    <div className="stack">
      <div className="stat-cards">
        <div className="card stat-card">
          <div className="stat-card__value">{leaderboard.data?.totalCount ?? 0}</div>
          <div className="stat-card__label">Репозиториев в рейтинге</div>
        </div>
        <div className="card stat-card">
          <div className="stat-card__value">{analyzed}</div>
          <div className="stat-card__label">Проанализировано на странице</div>
        </div>
        <div className="card stat-card">
          <div className="stat-card__value">{highestScore}</div>
          <div className="stat-card__label">Максимальный Score</div>
        </div>
      </div>

      <div className="row rating-tagline">
        <div className="tagline-chip">
          <span className="tagline-chip__ink">Код есть</span>
          <span className="tagline-chip__red">А насколько он «живой»?</span>
        </div>
        <div className="tagline-chip tagline-chip_logo" title="SourceCraft">
          <img src="/assets/sourcecraft-icon-96.png" alt="SourceCraft" />
        </div>
      </div>

      <div className="card stack rating-card">
        <LanguageTabs
          languages={languageOptions}
          selected={selectedLanguages}
          onSelectionChange={handleLanguagesChange}
        />

        <div className="row" style={{ justifyContent: "space-between" }}>
          <Dropdown
            value={sort === "score" ? "По Score" : sort === "likes" ? "По лайкам" : "По активности"}
            selectedOptions={[sort]}
            onOptionSelect={(_event, data) => {
              setSort(data.optionValue as string);
              setPage(1);
            }}
          >
            <Option value="score">По Score</Option>
            <Option value="likes">По лайкам</Option>
            <Option value="activity">По активности</Option>
          </Dropdown>
          <Button
            icon={refresh.isPending ? <Spinner size="tiny" /> : <ArrowSync24Regular />}
            onClick={() => refresh.mutate()}
            disabled={refresh.isPending}
          >
            {refresh.isPending ? "Обновление…" : "Обновить"}
          </Button>
        </div>
        <div className="row rating-filters">
          <label className="filter-field">
            <span className="muted">CI/CD</span>
            <Dropdown
              value={hasCi === "all" ? "Все" : hasCi === "yes" ? "Есть" : "Нет"}
              selectedOptions={[hasCi]}
              onOptionSelect={(_event, data) => handleHasCiChange(data.optionValue as "all" | "yes" | "no")}
            >
              <Option value="all">Все</Option>
              <Option value="yes">Есть</Option>
              <Option value="no">Нет</Option>
            </Dropdown>
          </label>
          <label className="filter-field">
            <span className="muted">Score от</span>
            <Input
              type="number"
              min={0}
              max={100}
              value={minScore}
              onChange={(_event, data) => handleMinScoreChange(data.value)}
            />
          </label>
          <label className="filter-field">
            <span className="muted">Score до</span>
            <Input
              type="number"
              min={0}
              max={100}
              value={maxScore}
              onChange={(_event, data) => handleMaxScoreChange(data.value)}
            />
          </label>
        </div>
        {refresh.isSuccess && refresh.data ? (
          <span className="tone-good">Обновлено: {refresh.data.refreshed}</span>
        ) : null}
        {refreshErrorMessage ? <span className="tone-bad">{refreshErrorMessage}</span> : null}
        {analyzeError ? <span className="tone-bad">{analyzeError}</span> : null}

        {leaderboard.isPending ? <LoadingView /> : null}
        {leaderboard.isError ? <ErrorView message="Не удалось загрузить рейтинг" /> : null}

        {leaderboard.isSuccess ? (
          <Table aria-label="Рейтинг репозиториев" className="rating-table">
            <TableHeader>
              <TableRow>
                <TableHeaderCell>#</TableHeaderCell>
                <TableHeaderCell>Проект</TableHeaderCell>
                <TableHeaderCell>Score</TableHeaderCell>
                <TableHeaderCell>Лайки</TableHeaderCell>
                <TableHeaderCell>Язык</TableHeaderCell>
                <TableHeaderCell>Активность</TableHeaderCell>
                <TableHeaderCell>Проанализирован</TableHeaderCell>
                <TableHeaderCell>Статус анализа</TableHeaderCell>
                <TableHeaderCell>Достоверность</TableHeaderCell>
                <TableHeaderCell aria-label="Действия" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((item) => (
                <RepoContextMenu key={item.sourceCraftId} url={item.url} fullName={item.fullName}>
                  <TableRow>
                    <TableCell className="rating-cell--place">
                      <div className="row" style={{ gap: "0.35rem", flexWrap: "nowrap" }}>
                        <span>{item.place}</span>
                        <PlaceDelta delta={item.placeDelta} />
                      </div>
                    </TableCell>
                    <TableCell className="rating-cell--project">
                      <Link to={`/repositories/${item.sourceCraftId}`}>{item.fullName}</Link>
                    </TableCell>
                    <TableCell className="rating-cell--score">
                      <strong style={{ color: item.score === null ? "var(--srhc-muted)" : toneColor(scoreTone(item.score)) }}>
                        {item.score ?? "—"}
                      </strong>
                    </TableCell>
                    <TableCell data-label="Лайки">{item.likesCount}</TableCell>
                    <TableCell data-label="Язык">
                      <span className="lang-cell">{languageDisplayName(item.language)}</span>
                    </TableCell>
                    <TableCell data-label="Активность" title={formatDate(item.lastActivityAt)}>
                      {formatDateShort(item.lastActivityAt)}
                    </TableCell>
                    <TableCell data-label="Проанализирован" title={formatDate(item.analyzedAt)}>
                      {formatDate(item.analyzedAt)}
                    </TableCell>
                    <TableCell data-label="Статус анализа">
                      <RepoStatus sourceCraftId={item.sourceCraftId} score={item.score} analyzedAt={item.analyzedAt} />
                    </TableCell>
                    <TableCell data-label="Достоверность">
                      <IntegrityBadge sourceCraftId={item.sourceCraftId} />
                    </TableCell>
                    <TableCell className="rating-cell--actions">
                      <div className="row" style={{ gap: "0.35rem", flexWrap: "nowrap" }}>
                        <Button
                          size="small"
                          appearance="primary"
                          icon={analyze.isPending && analyze.variables === item.sourceCraftId ? <Spinner size="tiny" /> : <Play24Regular />}
                          disabled={analyze.isPending}
                          onClick={() => runAnalyze(item.sourceCraftId)}
                        >
                          Анализировать
                        </Button>
                        <RepoActionsButton url={item.url} fullName={item.fullName} />
                      </div>
                    </TableCell>
                  </TableRow>
                </RepoContextMenu>
              ))}
            </TableBody>
          </Table>
        ) : null}

        <div className="row" style={{ justifyContent: "space-between" }}>
          <Button
            appearance="subtle"
            icon={<ArrowLeft24Regular />}
            disabled={page <= 1}
            onClick={() => setPage((value) => Math.max(1, value - 1))}
          >
            Назад
          </Button>
          <span className="muted">Страница {page}</span>
          <Button
            appearance="subtle"
            icon={<ArrowRight24Regular />}
            iconPosition="after"
            disabled={(leaderboard.data?.items.length ?? 0) < appConfig.leaderboardPageSize}
            onClick={() => setPage((value) => value + 1)}
          >
            Вперёд
          </Button>
        </div>
      </div>

      <span className="muted">
        Как считается Score? <Link to="/methodology">Методика Repo Health Score</Link>
      </span>
    </div>
  );
}
