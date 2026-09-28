import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  Badge,
  Button,
  Card,
  Input,
  Table,
  TableBody,
  TableCell,
  TableCellLayout,
  TableHeader,
  TableHeaderCell,
  TableRow
} from "@fluentui/react-components";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis
} from "recharts";
import { useComparison, useLeaderboard } from "../shared/api/hooks";
import type { CategoryScore, RepositoryComparisonItem } from "../shared/api/types";
import {
  categoryLabels,
  categoryOrder,
  formatDate,
  languageDisplayName,
  scoreTone,
  toneColor
} from "../shared/api/labels";
import { metricLabel } from "../shared/api/metrics";
import { ErrorView, LoadingView } from "../shared/ui/Status";

const MaximumSelection = 4;
const chartPalette = ["#2e7d32", "#f9a825", "#c62828", "#4527a0"];

function formatMetricValue(raw: number): string {
  return Number.isInteger(raw) ? String(raw) : raw.toFixed(2);
}

function renderCategoryChips(categories: CategoryScore[]) {
  if (categories.length === 0)
    return <span className="muted">—</span>;

  return (
    <div className="row">
      {categories.map((category) => (
        <span className={`pill pill--${scoreTone(category.score)}`} key={category.category}>
          {categoryLabels[category.category]} {category.score}
        </span>
      ))}
    </div>
  );
}

export function ComparePage() {
  const [search, setSearch] = useState("");
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const candidates = useLeaderboard({ pageSize: 100, sort: "score" });
  const comparison = useComparison(selectedIds);

  const candidateItems = useMemo(() => {
    const term = search.trim().toLowerCase();
    const items = candidates.data?.items ?? [];
    if (!term)
      return items;
    return items.filter(
      (item) =>
        item.fullName.toLowerCase().includes(term) ||
        item.name.toLowerCase().includes(term) ||
        item.language.toLowerCase().includes(term)
    );
  }, [candidates.data, search]);

  const toggle = (sourceCraftId: string) => {
    setSelectedIds((current) => {
      if (current.includes(sourceCraftId))
        return current.filter((id) => id !== sourceCraftId);
      if (current.length >= MaximumSelection)
        return current;
      return [...current, sourceCraftId];
    });
  };

  const items = comparison.data?.items ?? [];

  const chartData = useMemo(
    () =>
      categoryOrder.map((category) => {
        const row: Record<string, string | number> = { category: categoryLabels[category] };
        for (const item of comparison.data?.items ?? []) {
          const categoryScore = item.categories.find((entry) => entry.category === category);
          row[item.sourceCraftId] =
            categoryScore && categoryScore.dataStatus === "Available" ? categoryScore.score : 0;
        }
        return row;
      }),
    [comparison.data]
  );

  const metricCodes = useMemo(() => {
    const codes: string[] = [];
    for (const item of items)
      for (const metric of item.metrics)
        if (!codes.includes(metric.code))
          codes.push(metric.code);
    return codes;
  }, [items]);

  const renderSummaryCard = (item: RepositoryComparisonItem) => (
    <Card className="card stack" key={item.sourceCraftId}>
      <div className="row" style={{ justifyContent: "space-between" }}>
        <Link to={`/repositories/${item.sourceCraftId}`}>{item.fullName}</Link>
        <strong style={{ color: item.score === null ? "var(--srhc-muted)" : toneColor(scoreTone(item.score)) }}>
          {item.score ?? "—"}
        </strong>
      </div>
      <div className="repo-meta muted">
        <span className="lang-cell">{languageDisplayName(item.language)}</span>
        <span>Лайки: {item.likesCount}</span>
        <span>Активность: {formatDate(item.lastActivityAt)}</span>
        <span>Проанализирован: {formatDate(item.analyzedAt)}</span>
      </div>
      <div className="stack" style={{ gap: "0.35rem" }}>
        <span className="muted">Сильные стороны</span>
        {renderCategoryChips(item.strengths)}
      </div>
      <div className="stack" style={{ gap: "0.35rem" }}>
        <span className="muted">Слабые стороны</span>
        {renderCategoryChips(item.weaknesses)}
      </div>
    </Card>
  );

  return (
    <div className="stack">
      <Card className="card stack">
        <div style={{ fontWeight: 600 }}>Сравнение репозиториев</div>
        <p className="muted" style={{ margin: 0 }}>
          Выберите от 2 до 4 публичных репозиториев — покажем баллы, категории и ключевые метрики рядом.
        </p>
        <Input
          value={search}
          onChange={(_event, data) => setSearch(data.value)}
          placeholder="Поиск по названию или языку"
          aria-label="Поиск репозитория"
        />
        {candidates.isPending ? <LoadingView /> : null}
        {candidates.isError ? <ErrorView message="Не удалось загрузить список репозиториев." /> : null}
        <div className="stack" style={{ maxHeight: "18rem", overflowY: "auto" }}>
          {candidateItems.map((repository) => {
            const selected = selectedIds.includes(repository.sourceCraftId);
            return (
              <div className="metric-row repo-row" key={repository.sourceCraftId}>
                <div className="stack" style={{ gap: "0.35rem", minWidth: 0 }}>
                  <Link to={`/repositories/${repository.sourceCraftId}`}>{repository.fullName}</Link>
                  <div className="repo-meta muted">
                    <span className="lang-cell">{languageDisplayName(repository.language)}</span>
                    <span>Лайки: {repository.likesCount}</span>
                    <span>Score: {repository.score ?? "—"}</span>
                    <span>Активность: {formatDate(repository.lastActivityAt)}</span>
                  </div>
                </div>
                <Button
                  appearance={selected ? "primary" : "secondary"}
                  onClick={() => toggle(repository.sourceCraftId)}
                  disabled={!selected && selectedIds.length >= MaximumSelection}
                >
                  {selected ? "Убрать" : "Добавить"}
                </Button>
              </div>
            );
          })}
          {!candidates.isPending && candidateItems.length === 0 ? <span className="muted">Ничего не найдено.</span> : null}
        </div>
      </Card>

      {selectedIds.length < 2 ? (
        <Card className="card">
          <span className="muted">Выберите минимум два репозитория, чтобы построить сравнение.</span>
        </Card>
      ) : null}

      {comparison.isError ? (
        <Card className="card">
          <span className="tone-bad">Не удалось получить сравнение. Попробуйте ещё раз.</span>
        </Card>
      ) : null}

      {selectedIds.length >= 2 && comparison.isPending ? (
        <Card className="card">
          <LoadingView />
        </Card>
      ) : null}

      {items.length >= 2 ? (
        <>
          <div className="grid-2">{items.map(renderSummaryCard)}</div>

          <Card className="card stack">
            <div style={{ fontWeight: 600 }}>Оценка по категориям</div>
            <div style={{ width: "100%", height: "20rem" }}>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={chartData} margin={{ top: 8, right: 16, bottom: 0, left: -16 }}>
                  <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                  <XAxis dataKey="category" />
                  <YAxis domain={[0, 100]} />
                  <Tooltip />
                  <Legend />
                  {items.map((item, index) => (
                    <Bar
                      key={item.sourceCraftId}
                      dataKey={item.sourceCraftId}
                      name={item.fullName}
                      fill={chartPalette[index % chartPalette.length]}
                    />
                  ))}
                </BarChart>
              </ResponsiveContainer>
            </div>
          </Card>

          <Card className="card stack">
            <div style={{ fontWeight: 600 }}>Ключевые метрики</div>
            <Table size="small" aria-label="Сравнение метрик">
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Метрика</TableHeaderCell>
                  {items.map((item) => (
                    <TableHeaderCell key={item.sourceCraftId}>{item.fullName}</TableHeaderCell>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {metricCodes.map((code) => (
                  <TableRow key={code}>
                    <TableCell>{metricLabel(code)}</TableCell>
                    {items.map((item) => {
                      const metric = item.metrics.find((entry) => entry.code === code);
                      return (
                        <TableCell key={item.sourceCraftId}>
                          <TableCellLayout truncate>
                            {metric ? `${formatMetricValue(metric.rawValue)} (${Math.round(metric.normalizedScore)})` : "—"}
                          </TableCellLayout>
                        </TableCell>
                      );
                    })}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            <span className="muted">Формат: «сырое значение (нормализованный балл 0–100)».</span>
          </Card>

          <Card className="card stack">
            <div style={{ fontWeight: 600 }}>Итоговые баллы</div>
            <div className="stack">
              {items.map((item) => (
                <div className="metric-row" key={item.sourceCraftId}>
                  <Link to={`/repositories/${item.sourceCraftId}`}>{item.fullName}</Link>
                  <Badge appearance="tint" color={item.score === null ? "informative" : scoreTone(item.score) === "good" ? "success" : scoreTone(item.score) === "warn" ? "warning" : "danger"}>
                    {item.score ?? "Нет данных"}
                  </Badge>
                </div>
              ))}
            </div>
          </Card>
        </>
      ) : null}
    </div>
  );
}
