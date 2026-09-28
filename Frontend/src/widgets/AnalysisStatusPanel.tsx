import { useEffect, useState } from "react";
import { Button, Card, ProgressBar, Spinner } from "@fluentui/react-components";
import { ArrowSync24Regular, Dismiss24Regular } from "@fluentui/react-icons";
import type { AnalysisStatus, AnalysisStatusValue } from "../shared/api/types";
import { formatDate } from "../shared/api/labels";
import { AnalysisStatusBadge } from "./AnalysisStatusBadge";

export type AnalysisPanelPhase = AnalysisStatusValue | "idle";

interface AnalysisStatusPanelProps {
  repositoryId: string;
  status: AnalysisStatus | undefined;
  phase: AnalysisPanelPhase;
  score: number | null;
  errorMessage?: string | null;
  onRetry?: () => void;
  onClose: () => void;
}

const panelStyles = `
.srhc-status-dock {
  position: fixed;
  right: 1.25rem;
  bottom: 1.25rem;
  width: min(23rem, calc(100vw - 2.5rem));
  z-index: 50;
  animation: srhc-status-in 0.25s ease both;
}

.srhc-status-dock .srhc-status-dock__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.srhc-status-dock .srhc-status-dock__actions {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

@media (max-width: 48rem) {
  .srhc-status-dock {
    right: 0.75rem;
    bottom: 0.75rem;
    left: 0.75rem;
    width: auto;
  }
}

@keyframes srhc-status-in {
  from {
    opacity: 0;
    transform: translateY(0.5rem);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
`;

function buildStatus(repositoryId: string, phase: AnalysisStatusValue, score: number | null): AnalysisStatus {
  return { repositoryId, status: phase, score, updatedAt: null };
}

const analysisStages = [
  "Активность и коммиты",
  "Контрибьюторы",
  "Безопасность (AppSec)",
  "Документация",
  "CI/CD",
  "Issues",
  "Структура и файлы"
];

export function AnalysisStatusPanel({
  repositoryId,
  status,
  phase,
  score,
  errorMessage,
  onRetry,
  onClose
}: AnalysisStatusPanelProps) {
  const [elapsedSeconds, setElapsedSeconds] = useState(0);

  useEffect(() => {
    if (phase !== "running") {
      setElapsedSeconds(0);
      return;
    }

    const interval = window.setInterval(() => setElapsedSeconds((value) => value + 1), 1000);
    return () => window.clearInterval(interval);
  }, [phase]);

  if (phase === "idle")
    return null;

  const effectiveStatus = status ?? buildStatus(repositoryId, phase, score);
  const effectiveScore = effectiveStatus.score ?? score;

  return (
    <>
      <style>{panelStyles}</style>
      <div className="srhc-status-dock" role="status" aria-live="polite">
        <Card className="card stack">
          <div className="srhc-status-dock__head">
            <strong>Статус анализа</strong>
            <div className="srhc-status-dock__actions">
              <AnalysisStatusBadge status={effectiveStatus} score={effectiveScore} />
              <Button
                appearance="subtle"
                size="small"
                icon={<Dismiss24Regular />}
                aria-label="Скрыть панель статуса"
                onClick={onClose}
              />
            </div>
          </div>

          {phase === "queued" ? <span className="muted">Анализ поставлен в очередь…</span> : null}

          {phase === "running" ? (
            <>
              <ProgressBar />
              <span className="muted">
                Идёт анализ… {elapsedSeconds} с. Что сейчас изучается:
              </span>
              <ul className="status-stages">
                {analysisStages.map((stage) => (
                  <li key={stage}>
                    <Spinner size="tiny" />
                    {stage}
                  </li>
                ))}
              </ul>
            </>
          ) : null}

          {phase === "completed" ? (
            <span>
              Готово{effectiveScore === null ? "" : ` · ${effectiveScore}`}. Оценка и рекомендации на странице обновлены.
            </span>
          ) : null}

          {phase === "failed" ? (
            <div className="stack" style={{ gap: "0.5rem" }}>
              <span className="tone-bad">{errorMessage ?? "Не удалось выполнить анализ."}</span>
              {onRetry ? (
                <Button appearance="primary" icon={<ArrowSync24Regular />} onClick={onRetry}>
                  Повторить анализ
                </Button>
              ) : null}
            </div>
          ) : null}

          {status?.updatedAt ? (
            <span className="muted" style={{ fontSize: "0.8rem" }}>Обновлено: {formatDate(status.updatedAt)}</span>
          ) : null}
        </Card>
      </div>
    </>
  );
}
