import { useState } from "react";
import { Button, Card } from "@fluentui/react-components";
import { Checkmark16Regular, Copy16Regular } from "@fluentui/react-icons";
import { api } from "../shared/api/client";
import { usePublicScore } from "../shared/api/hooks";

export function PublicBadgeCard({ repositoryId }: { repositoryId: string }) {
  const badgeUrl = api.publicBadgeUrl(repositoryId);
  const scoreUrl = api.publicScoreUrl(repositoryId);
  const badgeMarkdown = `![repo health](${badgeUrl})`;
  const publicScore = usePublicScore(repositoryId);
  const [badgeError, setBadgeError] = useState(false);
  const [copied, setCopied] = useState(false);

  const copyMarkdown = async () => {
    try {
      await navigator.clipboard.writeText(badgeMarkdown);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };

  return (
    <Card className="card stack">
      <div style={{ fontWeight: 600 }}>Публичный бейдж и API</div>

      {badgeError ? (
        <span className="muted">Бейдж доступен только для публичных репозиториев.</span>
      ) : (
        <div className="badge-card__preview">
          <a href={badgeUrl} target="_blank" rel="noreferrer" title="Открыть SVG-бейдж">
            <img
              className="badge-card__image"
              src={badgeUrl}
              alt="Repo Health badge"
              onError={() => setBadgeError(true)}
            />
          </a>
        </div>
      )}

      <div className="badge-card__snippet">
        <span className="muted">Markdown для README:</span>
        <code className="badge-card__code">{badgeMarkdown}</code>
        <Button
          size="small"
          appearance="secondary"
          icon={copied ? <Checkmark16Regular /> : <Copy16Regular />}
          onClick={copyMarkdown}
        >
          {copied ? "Скопировано" : "Скопировать"}
        </Button>
      </div>

      <div className="badge-card__snippet">
        <span className="muted">JSON Score:</span>
        <a className="badge-card__link" href={scoreUrl} target="_blank" rel="noreferrer">
          {scoreUrl}
        </a>
        <Button size="small" appearance="subtle" as="a" href={scoreUrl} target="_blank" rel="noreferrer">
          Открыть
        </Button>
      </div>

      {publicScore.isPending ? (
        <span className="muted">Проверяем публичный Score…</span>
      ) : publicScore.isError ? (
        <span className="muted">Публичная оценка недоступна — возможно, репозиторий приватный.</span>
      ) : publicScore.data ? (
        <div className="metric-row">
          <span>Публичный Score · методика v{publicScore.data.methodologyVersion}</span>
          <strong>
            {publicScore.data.score ?? "—"}
            {publicScore.data.grade ? ` · ${publicScore.data.grade}` : ""}
          </strong>
        </div>
      ) : null}
    </Card>
  );
}
