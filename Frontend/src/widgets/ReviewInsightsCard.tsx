import {
  Badge,
  Card,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow
} from "@fluentui/react-components";
import { useReviewInsights } from "../shared/api/hooks";

function formatDays(value: number | null | undefined): string {
  if (value === null || value === undefined)
    return "—";
  return `${value.toFixed(1)} дн.`;
}

function formatShare(value: number): string {
  return `${Math.round(value * 100)}%`;
}

export function ReviewInsightsCard({ repositoryId }: { repositoryId: string }) {
  const insights = useReviewInsights(repositoryId);
  const result = insights.data;
  const data = result?.status === "Available" ? result.data : null;

  return (
    <Card className="card stack">
      <div style={{ fontWeight: 600 }}>Ревью и bottlenecks</div>
      {insights.isPending ? (
        <div className="chart-loading">
          <Spinner size="small" />
          <span className="muted">Считаем метрики ревью…</span>
        </div>
      ) : insights.isError ? (
        <span className="muted">Не удалось загрузить данные о ревью.</span>
      ) : !data ? (
        <span className="muted">{result?.reason || "Нет данных о ревью."}</span>
      ) : data.totalMergeRequests === 0 ? (
        <span className="muted">В репозитории нет merge request&apos;ов.</span>
      ) : (
        <>
          <div className="row" style={{ gap: "0.5rem" }}>
            <Badge appearance="tint">MR: {data.totalMergeRequests}</Badge>
            <Badge appearance="tint" color="informative">Оценено: {data.evaluatedMergeRequests}</Badge>
            <Badge appearance="tint" color={data.mergeRequestsWithoutReview > 0 ? "warning" : "success"}>
              Без ревью: {data.mergeRequestsWithoutReview}
            </Badge>
          </div>
          <div className="metric-row">
            <span>Среднее время до первого ревью</span>
            <strong>{formatDays(data.averageTimeToFirstReviewDays)}</strong>
          </div>
          <div className="metric-row">
            <span>Медианное время до первого ревью</span>
            <strong>{formatDays(data.medianTimeToFirstReviewDays)}</strong>
          </div>
          <div className="metric-row">
            <span>Доля MR с комментариями</span>
            <strong>{formatShare(data.shareOfCommentedMergeRequests)}</strong>
          </div>
          <div>
            <div style={{ fontWeight: 600, marginBottom: "0.35rem" }}>Нагрузка на авторов MR</div>
            {data.reviewerLoad.length === 0 ? (
              <span className="muted">Нет данных по авторам MR.</span>
            ) : (
              <div className="review-load-table">
                <Table size="small" aria-label="Нагрузка на авторов merge request'ов">
                  <TableHeader>
                    <TableRow>
                      <TableHeaderCell>Автор</TableHeaderCell>
                      <TableHeaderCell>MR</TableHeaderCell>
                      <TableHeaderCell>С ревью</TableHeaderCell>
                      <TableHeaderCell>Без ревью</TableHeaderCell>
                      <TableHeaderCell>Ср. время</TableHeaderCell>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.reviewerLoad.map((item) => (
                      <TableRow key={item.authorLogin}>
                        <TableCell>{item.authorLogin || "—"}</TableCell>
                        <TableCell>{item.mergeRequestCount}</TableCell>
                        <TableCell>{item.reviewedMergeRequestCount}</TableCell>
                        <TableCell className={item.unreviewedMergeRequestCount > 0 ? "tone-warn" : undefined}>
                          {item.unreviewedMergeRequestCount}
                        </TableCell>
                        <TableCell>{formatDays(item.averageTimeToFirstReviewDays)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </div>
        </>
      )}
    </Card>
  );
}
