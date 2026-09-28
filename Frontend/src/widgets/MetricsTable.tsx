import {
  Table,
  TableBody,
  TableCell,
  TableCellLayout,
  TableHeader,
  TableHeaderCell,
  TableRow
} from "@fluentui/react-components";
import type { MetricScore } from "../shared/api/types";
import { dataStatusLabel, scoreTone, toneColor } from "../shared/api/labels";
import { metricLabel } from "../shared/api/metrics";

const metricValue = (raw: number): string => (Number.isInteger(raw) ? String(raw) : raw.toFixed(2));

export function MetricsTable({ metrics }: { metrics: MetricScore[] }) {
  if (metrics.length === 0)
    return <span className="muted">Метрики отсутствуют.</span>;

  return (
    <Table size="small" aria-label="Метрики оценки">
      <TableHeader>
        <TableRow>
          <TableHeaderCell>Метрика</TableHeaderCell>
          <TableHeaderCell>Код</TableHeaderCell>
          <TableHeaderCell>Значение</TableHeaderCell>
          <TableHeaderCell>Нормализация</TableHeaderCell>
          <TableHeaderCell>Вес</TableHeaderCell>
          <TableHeaderCell>Данные</TableHeaderCell>
        </TableRow>
      </TableHeader>
      <TableBody>
        {metrics.map((metric) => (
          <TableRow key={metric.code}>
            <TableCell>
              <TableCellLayout truncate>{metricLabel(metric.code)}</TableCellLayout>
            </TableCell>
            <TableCell>
              <span className="muted" style={{ fontSize: "0.8rem" }}>{metric.code}</span>
            </TableCell>
            <TableCell>{metricValue(metric.rawValue)}</TableCell>
            <TableCell>
              <strong style={{ color: toneColor(scoreTone(metric.normalizedScore)) }}>
                {metric.normalizedScore.toFixed(1)}
              </strong>
            </TableCell>
            <TableCell>{metric.weight}</TableCell>
            <TableCell>{dataStatusLabel(metric.dataStatus)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
