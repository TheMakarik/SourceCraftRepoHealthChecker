import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  Button,
  Card,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Spinner
} from "@fluentui/react-components";
import { useAnalyze, useMe, useMeTokens, useMyRepositories } from "../shared/api/hooks";
import { formatDate, languageDisplayName } from "../shared/api/labels";
import { useAnalysisStatus } from "../shared/api/useAnalysisStatus";
import { Box24Regular, Play24Regular } from "@fluentui/react-icons";
import { ApiError } from "../shared/api/client";
import { ErrorView, LoadingView } from "../shared/ui/Status";
import { AnalysisStatusBadge } from "../widgets/AnalysisStatusBadge";
import { LoginGate } from "../app/LoginGate";
import { RepoActionsButton, RepoContextMenu } from "../widgets/RepoContextMenu";

export function RepositoriesPage() {
  const me = useMe();
  const isAuthenticated = me.isSuccess;
  const isAnonymous = !me.isPending && !me.isSuccess;
  const tokens = useMeTokens(isAuthenticated);
  const hasPat = Boolean(tokens.data?.sourceCraftTokenPrefix);
  const mine = useMyRepositories(isAuthenticated && hasPat);
  const analyze = useAnalyze();
  const analysisStatus = useAnalysisStatus();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const [dismissed, setDismissed] = useState(false);

  const unauthorized = mine.error instanceof ApiError && mine.error.status === 401;
  const needPat = isAuthenticated && ((tokens.isSuccess && !hasPat) || unauthorized);
  const dialogOpen = needPat && !dismissed;

  const runAnalyze = (id: string) => {
    setError(null);
    analyze.mutate(id, {
      onSuccess: () => navigate(`/repositories/${id}`),
      onError: (exception) => {
        const status = exception instanceof ApiError ? exception.status : undefined;
        if (status === 401)
          setError("Нужен PAT — добавьте его в настройках.");
        else if (status === 404)
          setError("SourceCraft не отдал репозиторий — проверьте PAT в настройках.");
        else
          setError("Не удалось запустить анализ. Попробуйте позже.");
      }
    });
  };

  return (
    <>
      <div className={`stack${needPat ? " dimmed" : ""}`} aria-hidden={needPat}>
        <Card className="card stack">
          <div style={{ fontWeight: 600, display: "flex", alignItems: "center", gap: "0.4rem" }}>
            <Box24Regular /> Мои репозитории
          </div>
          <p className="muted" style={{ margin: 0 }}>
            Список ваших репозиториев SourceCraft. Откройте репозиторий, чтобы посмотреть анализ, или запустите новый.
          </p>
        </Card>

        {me.isPending ? (
          <Card className="card stack">
            <LoadingView />
          </Card>
        ) : isAnonymous ? (
          <LoginGate inline />
        ) : (
          <Card className="card stack">
            {tokens.isPending || mine.isPending ? <LoadingView /> : null}
            {mine.isError && !unauthorized ? <ErrorView message="Не удалось получить список — попробуйте позже." /> : null}
            {error ? <span className="tone-bad">{error}</span> : null}
            {mine.data?.length === 0 ? <span className="muted">Репозитории не найдены.</span> : null}
            {mine.data?.map((repository) => (
              <RepoContextMenu key={repository.id} url={repository.url} fullName={repository.fullName}>
                <div className="metric-row repo-row">
                  <div className="stack" style={{ gap: "0.35rem", minWidth: 0 }}>
                    <div className="row" style={{ gap: "0.5rem" }}>
                      <Link to={`/repositories/${repository.id}`}>{repository.fullName}</Link>
                      <AnalysisStatusBadge status={analysisStatus.statusFor(repository.id)} />
                    </div>
                    <div className="repo-meta muted">
                      <span className="lang-cell">{languageDisplayName(repository.language)}</span>
                      <span>{repository.isPrivate ? "приватный" : "публичный"}</span>
                      <span>Ветка: {repository.defaultBranch || "—"}</span>
                      <span>Лайки: {repository.likesCount}</span>
                      <span>Активность: {formatDate(repository.lastActivityAt)}</span>
                    </div>
                  </div>
                  <div className="row">
                    <RepoActionsButton url={repository.url} fullName={repository.fullName} />
                    <Button
                      appearance="primary"
                      icon={<Play24Regular />}
                      disabled={analyze.isPending}
                      onClick={() => runAnalyze(repository.id)}
                    >
                      {analyze.isPending && analyze.variables === repository.id ? <Spinner size="tiny" /> : "Анализировать"}
                    </Button>
                  </div>
                </div>
              </RepoContextMenu>
            ))}
          </Card>
        )}
      </div>

      <Dialog open={dialogOpen} modalType="modal">
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Нужен PAT SourceCraft</DialogTitle>
            <DialogContent>
              Чтобы видеть и анализировать ваши репозитории, добавьте персональный токен SourceCraft (PAT) в настройках.
            </DialogContent>
            <DialogActions>
              <Button
                appearance="primary"
                onClick={() => {
                  setDismissed(true);
                  navigate("/settings");
                }}
              >
                Перейти в настройки
              </Button>
              <Button appearance="secondary" onClick={() => setDismissed(true)}>
                Отмена
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  );
}
