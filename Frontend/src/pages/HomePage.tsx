import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
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
import { useLanguages, useLeaderboard, useRefresh } from "../shared/api/hooks";
import { formatDate, formatDateShort, languageDisplayName, scoreTone, toneColor } from "../shared/api/labels";
import { appConfig } from "../shared/config";
import { ErrorView, LoadingView } from "../shared/ui/Status";
import { LanguageTabs } from "../widgets/LanguageTabs";
import { RepoActionsButton, RepoContextMenu } from "../widgets/RepoContextMenu";

export function HomePage() {
  const [selectedLanguages, setSelectedLanguages] = useState<string[]>([]);
  const [sort, setSort] = useState<string>("score");
  const [page, setPage] = useState(1);
  const languageFilter = selectedLanguages.length === 1 ? selectedLanguages[0] : undefined;
  const query = useMemo(
    () => ({ language: languageFilter, sort, page, pageSize: appConfig.leaderboardPageSize }),
    [languageFilter, sort, page]
  );
  const leaderboard = useLeaderboard(query);
  const languages = useLanguages();
  const refresh = useRefresh();

  const languageOptions = languages.data ?? [];
  const items = useMemo(() => {
    const pageItems = leaderboard.data?.items ?? [];
    if (selectedLanguages.length <= 1)
      return pageItems;
    const selected = new Set(selectedLanguages.map((language) => language.toLowerCase()));
    return pageItems.filter((item) => selected.has(item.language.toLowerCase()));
  }, [leaderboard.data, selectedLanguages]);
  const highestScore = items.reduce((max, item) => Math.max(max, item.score ?? 0), 0);
  const analyzed = items.filter((item) => item.analyzedAt).length;

  const handleLanguagesChange = (next: string[]) => {
    setSelectedLanguages(next);
    setPage(1);
  };

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
