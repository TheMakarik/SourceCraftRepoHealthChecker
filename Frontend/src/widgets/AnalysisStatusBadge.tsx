import { Badge, Spinner } from "@fluentui/react-components";
import type { AnalysisStatus, AnalysisStatusValue } from "../shared/api/types";

type BadgeColor = "brand" | "danger" | "important" | "informative" | "severe" | "subtle" | "success" | "warning";

const statusLabels: Record<AnalysisStatusValue, string> = {
  queued: "В очереди",
  running: "Идёт анализ",
  completed: "Готово",
  failed: "Ошибка"
};

const statusColors: Record<AnalysisStatusValue, BadgeColor> = {
  queued: "informative",
  running: "brand",
  completed: "success",
  failed: "danger"
};

export function AnalysisStatusBadge({
  status,
  score
}: {
  status: AnalysisStatus | undefined;
  score?: number | null;
}) {
  if (!status)
    return null;

  const value = status.score ?? score ?? null;

  return (
    <Badge appearance="tint" color={statusColors[status.status]}>
      {status.status === "running" ? <Spinner size="tiny" /> : null}
      {statusLabels[status.status]}
      {value !== null ? ` · ${value}` : ""}
    </Badge>
  );
}
