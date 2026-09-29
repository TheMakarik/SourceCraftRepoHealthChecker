import { PlugDisconnected16Regular, QuestionCircle16Regular } from "@fluentui/react-icons";
import {
  Badge,
  Table,
  TableBody,
  TableCell,
  TableCellLayout,
  TableHeader,
  TableHeaderCell,
  TableRow
} from "@fluentui/react-components";
import type { Analysis, DataStatus, ScoreCategory } from "../shared/api/types";
import { dataStatusLabel, scoreGrade, scoreTone, severityTone, toneColor } from "../shared/api/labels";
import { metricLabel } from "../shared/api/metrics";
import { ScoreRing } from "./ScoreRing";

const prefixes: Record<ScoreCategory, string> = {
  Security: "Security",
  CodeHealth: "CodeHealth",
  Activity: "Activity",
  Documentation: "Documentation",
  CiCd: "CiCd",
  Issues: "Issues"
};

const metricValue = (raw: number): string => (Number.isInteger(raw) ? String(raw) : raw.toFixed(2));

export function CategoryPanel({ analysis, category }: { analysis: Analysis; category: ScoreCategory }) {
  const categoryScore = analysis.categories.find((item) => item.category === category);
  const metrics = analysis.metrics.filter(
    (metric) => metric.code.startsWith(prefixes[category]) && metric.dataStatus === "Available"
  );

  if (!categoryScore || categoryScore.dataStatus !== "Available") {
    const status: DataStatus = categoryScore?.dataStatus ?? "NoData";
    const isUnavailable = status === "Unavailable";

    return (
      <div className="category-panel">
        <Badge
          appearance="tint"
          color={isUnavailable ? "danger" : "warning"}
          icon={isUnavailable ? <PlugDisconnected16Regular /> : <QuestionCircle16Regular />}
        >
          {dataStatusLabel(status)}
        </Badge>
        <p className="muted">
          {isUnavailable
            ? "Источник данных недоступен — категория не штрафуется."
            : "Для этой категории недостаточно данных — оценка не штрафуется."}
        </p>
      </div>
    );
  }

  const tone = scoreTone(categoryScore.score);

  return (
    <div className="category-panel">
      <div className="category-panel__head">
        <div className="category-panel__ring">
          <ScoreRing score={categoryScore.score} size={140} thickness={12} grade={scoreGrade(categoryScore.score)} />
        </div>
        <div className="stack" style={{ flex: 1 }}>
          <span className={`pill pill--${tone}`}>Данные есть</span>
          {category === "Security" ? <SecurityDetails analysis={analysis} /> : null}
          {category !== "Security" ? (
            <div>
              {metrics.map((metric) => (
                <div className="metric-row" key={metric.code}>
                  <span>{metricLabel(metric.code)}</span>
                  <strong style={{ color: toneColor(scoreTone(metric.normalizedScore)) }}>
                    {metricValue(metric.rawValue)}
                  </strong>
                </div>
              ))}
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}

function SecurityDetails({ analysis }: { analysis: Analysis }) {
  const open = analysis.findings.filter((finding) => finding.status.toLowerCase() === "open");
  const fixed = analysis.findings.filter((finding) => finding.status.toLowerCase() === "fixed").length;

  return (
    <div className="stack">
      <div className="row">
        <Badge appearance="tint" color="danger">Открыто: {open.length}</Badge>
        <Badge appearance="tint" color="success">Исправлено: {fixed}</Badge>
      </div>
      {analysis.findings.length === 0 ? (
        <p className="muted">Находок безопасности нет.</p>
      ) : (
        <Table size="small" aria-label="Находки безопасности">
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Тип</TableHeaderCell>
              <TableHeaderCell>Правило</TableHeaderCell>
              <TableHeaderCell>Критичность</TableHeaderCell>
              <TableHeaderCell>CVSS</TableHeaderCell>
              <TableHeaderCell>Статус</TableHeaderCell>
              <TableHeaderCell>Файл</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {analysis.findings.map((finding, index) => (
              <TableRow key={`${finding.title}-${index}`}>
                <TableCell>{finding.kind}</TableCell>
                <TableCell>
                  <TableCellLayout truncate>{finding.title}</TableCellLayout>
                </TableCell>
                <TableCell>
                  <span className={`tone-${severityTone(finding.severity)}`}>{finding.severity}</span>
                </TableCell>
                <TableCell>{finding.cvssScore ?? "—"}</TableCell>
                <TableCell>{finding.status}</TableCell>
                <TableCell>
                  <TableCellLayout truncate>{finding.filePath ?? finding.package ?? "—"}</TableCellLayout>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
