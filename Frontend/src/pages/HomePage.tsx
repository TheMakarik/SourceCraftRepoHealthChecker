import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  Badge,
  Button,
  Dropdown,
  Option,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow
} from "@fluentui/react-components";
import { ArrowLeft24Regular, ArrowRight24Regular, ArrowSync24Regular } from "@fluentui/react-icons";
import { useLanguages, useLeaderboard, useRefresh, useRepositoryIntegrity } from "../shared/api/hooks";
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

export function HomePage() {
  const [selectedLanguages, setSelectedLanguages] = useState<string[]>([]);
  const [sort, setSort] = useState<string>("score");
  const [page, setPage] = useState(1);
  const query = useMemo(
    () => ({ languages: selectedLanguages, sort, page, pageSize: appConfig.leaderboardPageSize }),
    [selectedLanguages, sort, page]
  );
  const leaderboard = useLeaderboard(query);
  const languages = useLanguages();
  const refresh = useRefresh();

  const languageOptions = languages.data ?? [];
  const items = leaderboard.data?.items ?? [];
  const highestScore = items.reduce((max, item) => Math.max(max, item.score ?? 0), 0);
  const analyzed = items.filter((item) => item.analyzedAt).length;

  const handleLanguagesChange = (next: string[]) => {
    setSelectedLanguages(next);
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
        {refresh.isSuccess && refresh.data ? (
          <span className="tone-good">Обновлено: {refresh.data.refreshed}</span>
        ) : null}
        {refreshErrorMessage ? <span className="tone-bad">{refreshErrorMessage}</span> : null}

        {leaderboard.isPending ? <LoadingView /> : null}
        {leaderboard.isError ? <ErrorView message="Не удалось загрузить рейтинг" /> : null}

        {leaderboard.isSuccess ? (
          <Table aria-label="Рейтинг репозиториев">
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
                    <TableCell>{item.place}</TableCell>
                    <TableCell>
                      <Link to={`/repositories/${item.sourceCraftId}`}>{item.fullName}</Link>
                    </TableCell>
                    <TableCell>
                      <strong style={{ color: item.score === null ? "var(--srhc-muted)" : toneColor(scoreTone(item.score)) }}>
                        {item.score ?? "—"}
                      </strong>
                    </TableCell>
                    <TableCell>{item.likesCount}</TableCell>
                    <TableCell>
                      <span className="lang-cell">{languageDisplayName(item.language)}</span>
                    </TableCell>
                    <TableCell title={formatDate(item.lastActivityAt)}>{formatDateShort(item.lastActivityAt)}</TableCell>
                    <TableCell title={formatDate(item.analyzedAt)}>{formatDate(item.analyzedAt)}</TableCell>
                    <TableCell>
                      <RepoStatus sourceCraftId={item.sourceCraftId} score={item.score} analyzedAt={item.analyzedAt} />
                    </TableCell>
                    <TableCell>
                      <IntegrityBadge sourceCraftId={item.sourceCraftId} />
                    </TableCell>
                    <TableCell>
                      <RepoActionsButton url={item.url} fullName={item.fullName} />
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
