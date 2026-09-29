import { Fragment, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  Accordion,
  AccordionHeader,
  AccordionItem,
  AccordionPanel,
  Badge,
  Button,
  Card,
  Spinner
} from "@fluentui/react-components";
import { ArrowDownload24Regular, ArrowSync24Regular, Sparkle24Regular } from "@fluentui/react-icons";
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useAnalysis, useAnalyze, useHistory, useOwnership, useStructure } from "../shared/api/hooks";
import { useAnalysisStatus } from "../shared/api/useAnalysisStatus";
import type { AiInsightKind } from "../shared/api/types";
import {
  categoryLabels,
  hasAvailableCategories,
  priorityLabels,
  priorityTone,
  formatDate,
  formatDateShort,
  formatExpectedImpact,
  scoreGrade,
  scoreTone
} from "../shared/api/labels";
import { ErrorView, LoadingView } from "../shared/ui/Status";
import { Markdown } from "../shared/ui/Markdown";
import { appConfig } from "../shared/config";
import { ScoreRing } from "../widgets/ScoreRing";
import { CategoryCapsules } from "../widgets/CategoryCapsules";
import { CategoryTabs } from "../widgets/CategoryTabs";
import { AnalysisStatusBadge } from "../widgets/AnalysisStatusBadge";
import { AnalysisStatusPanel, type AnalysisPanelPhase } from "../widgets/AnalysisStatusPanel";
import { MetricsTable } from "../widgets/MetricsTable";
import { ReviewInsightsCard } from "../widgets/ReviewInsightsCard";
import { PublicBadgeCard } from "../widgets/PublicBadgeCard";
import { api, ApiError, streamAi } from "../shared/api/client";
import { insightLabels } from "../shared/api/labels";

const insightKinds: AiInsightKind[] = ["Recommendations", "Explanation", "ActionPlan", "SecurityTriage", "RiskForecast"];

export function DashboardPage() {
  const { id } = useParams<{ id: string }>();
  const analysis = useAnalysis(id);
  const structure = useStructure(id);
  const ownership = useOwnership(id);
  const history = useHistory(id);
  const analyze = useAnalyze();
  const { statusFor } = useAnalysisStatus();
  const [aiError, setAiError] = useState<string | null>(null);
  const [aiText, setAiText] = useState("");
  const [aiReasoning, setAiReasoning] = useState("");
  const [aiThinking, setAiThinking] = useState(false);
  const [aiRunning, setAiRunning] = useState(false);
  const [chartReady, setChartReady] = useState(false);
  const [statusOpen, setStatusOpen] = useState(false);

  const resetAnalyze = analyze.reset;
  const liveStatus = statusFor(id);

  useEffect(() => {
    setChartReady(false);
    const timer = window.setTimeout(() => setChartReady(true), appConfig.chartLoadDelayMs);
    return () => window.clearTimeout(timer);
  }, [id]);

  useEffect(() => {
    setStatusOpen(false);
    setAiError(null);
    setAiText("");
    setAiReasoning("");
    setAiThinking(false);
    resetAnalyze();
  }, [id, resetAnalyze]);

  useEffect(() => {
    if (liveStatus?.status === "queued" || liveStatus?.status === "running")
      setStatusOpen(true);
  }, [liveStatus?.status]);

  const phase: AnalysisPanelPhase = analyze.isPending
    ? "running"
    : analyze.isError
      ? "failed"
      : analyze.isSuccess
        ? "completed"
        : liveStatus?.status ?? "idle";

  const runAnalyze = () => {
    if (!id)
      return;
    setStatusOpen(true);
    setAiError(null);
    analyze.mutate(id);
  };

  const analyzeErrorMessage = analyze.error
    ? analyze.error instanceof ApiError
      ? analyze.error.status === 401
        ? "Нужен вход и PAT — настройте в личном кабинете."
        : analyze.error.status === 404
          ? "Репозиторий недоступен для анализа."
          : `Не удалось запустить анализ (HTTP ${analyze.error.status}).`
      : "Не удалось запустить анализ. Проверьте соединение и попробуйте снова."
    : null;

  const aiErrorMessage = (error: unknown, fallback: string) =>
    error instanceof ApiError && error.message ? error.message : fallback;

  const runAi = async (kind?: AiInsightKind) => {
    if (!id)
      return;
    setAiError(null);
    setAiText("");
    setAiReasoning("");
    setAiThinking(true);
    setAiRunning(true);
    const path = kind
      ? `/api/repositories/${encodeURIComponent(id)}/ai-insights/${kind}/stream`
      : `/api/repositories/${encodeURIComponent(id)}/ai-summary/stream`;
    try {
      await streamAi(path, (event) => {
        if (event.type === "delta" && event.text) {
          setAiThinking(false);
          setAiText((previous) => previous + event.text);
        } else if (event.type === "thinking" && event.text) {
          setAiThinking(true);
          setAiReasoning((previous) => previous + event.text);
        } else if (event.type === "reasoning") {
          setAiThinking(true);
        } else if (event.type === "error") {
          setAiError(event.text ?? "Ошибка AI-провайдера.");
        }
      });
    } catch (error) {
      setAiError(aiErrorMessage(error, "Не удалось получить ответ. Войдите и настройте AI в настройках."));
    } finally {
      setAiRunning(false);
      setAiThinking(false);
    }
  };

  const analysisHasData = analysis.data ? hasAvailableCategories(analysis.data.categories) : false;

  const statusPanel = id ? (
    <AnalysisStatusPanel
      repositoryId={id}
      status={liveStatus}
      phase={phase}
      score={liveStatus?.score ?? (analysisHasData ? analysis.data?.score ?? null : null)}
      errorMessage={analyzeErrorMessage}
      onRetry={runAnalyze}
      onClose={() => setStatusOpen(false)}
    />
  ) : null;

  const renderContent = () => {
    if (analysis.isLoading)
      return <LoadingView label="Загружаем анализ…" />;

    if (analysis.isError) {
      const status = analysis.error instanceof ApiError ? analysis.error.status : undefined;
      if (status === 404)
        return (
          <div className="stack">
            <ErrorView message="Репозиторий ещё не проанализирован." />
            <div className="row" style={{ alignItems: "center" }}>
              <Button
                appearance="primary"
                icon={analyze.isPending ? <Spinner size="tiny" /> : <ArrowSync24Regular />}
                disabled={analyze.isPending}
                onClick={runAnalyze}
              >
                {analyze.isPending ? "Анализируем…" : "Анализировать"}
              </Button>
              <span className="muted">Запустите анализ, чтобы рассчитать Repo Health Score.</span>
            </div>
            {analyzeErrorMessage ? <span className="tone-warn">{analyzeErrorMessage}</span> : null}
          </div>
        );
      if (status === 401)
        return <ErrorView message="Нужен вход и PAT — настройте в личном кабинете." />;
      return (
        <div className="stack">
          <ErrorView message="Не удалось загрузить анализ. Попробуйте обновить страницу позже." />
          <div className="row">
            <Button
              icon={analysis.isFetching ? <Spinner size="tiny" /> : <ArrowSync24Regular />}
              disabled={analysis.isFetching}
              onClick={() => analysis.refetch()}
            >
              Повторить
            </Button>
          </div>
        </div>
      );
    }

    if (!analysis.data)
      return <ErrorView message="Не удалось загрузить анализ." />;

    const data = analysis.data;
    const historyPoints = history.data ?? [];
    const hasData = hasAvailableCategories(data.categories);

    return (
      <div className="stack">
        <div className="grid-2">
          <Card className="card">
            <div className="row dashboard-head">
              <ScoreRing score={hasData ? data.score : null} grade={hasData ? scoreGrade(data.score) : undefined} />
              <div className="stack" style={{ flex: 1, minWidth: 0 }}>
                <div>
                  <div className="row" style={{ justifyContent: "space-between", gap: "0.75rem" }}>
                    <div style={{ fontSize: "1.1rem", fontWeight: 600 }}>{data.fullName}</div>
                    <AnalysisStatusBadge status={liveStatus} score={hasData ? data.score : null} />
                  </div>
                  <a className="muted" href={data.url} target="_blank" rel="noreferrer">
                    {data.url}
                  </a>
                </div>
                <div className="row">
                  <Badge appearance="tint">{data.language || "язык неизвестен"}</Badge>
                  <Badge appearance="tint">Лайки: {data.likesCount}</Badge>
                  {data.isPrivate ? <Badge appearance="tint" color="warning">Приватный</Badge> : null}
                </div>
                <span className="muted">
                  Последний анализ: {formatDate(data.analyzedAt)}
                  {analysis.isFetching ? " · обновляем данные…" : ""}
                </span>
                <div className="row">
                  <Button
                    appearance="secondary"
                    icon={analyze.isPending ? <Spinner size="tiny" /> : <ArrowSync24Regular />}
                    disabled={analyze.isPending}
                    onClick={runAnalyze}
                  >
                    {analyze.isPending ? "Анализируем…" : "Анализировать"}
                  </Button>
                  <Button
                    appearance="primary"
                    icon={aiRunning ? <Spinner size="tiny" /> : <Sparkle24Regular />}
                    disabled={aiRunning}
                    onClick={() => runAi()}
                  >
                    AI-резюме
                  </Button>
                  <Button as="a" href={api.reportUrl(data.sourceCraftId, "pdf")} download icon={<ArrowDownload24Regular />}>
                    PDF
                  </Button>
                  <Button as="a" href={api.reportMarkdownUrl(data.sourceCraftId)} download icon={<ArrowDownload24Regular />}>
                    Markdown
                  </Button>
                  <Button as="a" href={api.reportUrl(data.sourceCraftId, "json")} download icon={<ArrowDownload24Regular />} appearance="subtle">
                    JSON
                  </Button>
                  <Button as="a" href={api.reportUrl(data.sourceCraftId, "html")} download icon={<ArrowDownload24Regular />} appearance="subtle">
                    HTML
                  </Button>
                </div>
                {analyzeErrorMessage ? <span className="tone-warn">{analyzeErrorMessage}</span> : null}
              </div>
            </div>
          </Card>

          <Card className="card">
            <div style={{ marginBottom: "0.5rem", fontWeight: 600 }}>Оценка по категориям</div>
            <CategoryCapsules categories={data.categories} />
          </Card>
        </div>

        <CategoryTabs analysis={data} />

        <Card className="card stack">
          <div style={{ fontWeight: 600 }}>Динамика Score</div>
          {!chartReady || history.isPending ? (
            <div className="chart-loading">
              <Spinner size="small" />
              <span className="muted">Загружаем график…</span>
            </div>
          ) : history.isError ? (
            <span className="muted">Не удалось загрузить историю оценок.</span>
          ) : historyPoints.length >= 1 ? (
            <div style={{ width: "100%", height: "16rem" }}>
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={historyPoints} margin={{ top: 8, right: 16, bottom: 0, left: -16 }}>
                  <XAxis dataKey="analyzedAt" tickFormatter={(value) => formatDateShort(String(value))} />
                  <YAxis domain={[0, 100]} />
                  <Tooltip labelFormatter={(value) => formatDate(String(value))} />
                  <Line type="monotone" dataKey="score" name="Score" stroke="var(--srhc-good)" strokeWidth={2} dot />
                </LineChart>
              </ResponsiveContainer>
            </div>
          ) : (
            <span className="muted">Недостаточно данных для графика — нужен минимум ещё один анализ.</span>
          )}
        </Card>

        <Card className="card stack">
          <div style={{ fontWeight: 600 }}>Метрики</div>
          <MetricsTable metrics={data.metrics} />
        </Card>

        <Card className="card stack">
          <div style={{ fontWeight: 600 }}>Владельцы и bus factor</div>
          {ownership.isPending ? (
            <div className="chart-loading">
              <Spinner size="small" />
              <span className="muted">Считаем владельцев…</span>
            </div>
          ) : ownership.isError ? (
            <span className="muted">Не удалось загрузить данные о владельцах.</span>
          ) : ownership.data?.status === "Available" && ownership.data.data && ownership.data.data.owners.length > 0 ? (
            <>
              <div className="row" style={{ gap: "0.5rem" }}>
                <Badge appearance="tint" color={ownership.data.data.busFactor <= 1 ? "danger" : "informative"}>
                  Bus factor: {ownership.data.data.busFactor}
                </Badge>
                <Badge appearance="tint">Контрибьюторов: {ownership.data.data.totalContributors}</Badge>
              </div>
              <div className="owners-grid">
                <span className="muted">Владелец</span>
                <span className="muted">Коммиты</span>
                <span className="muted">Файлы</span>
                <span className="muted">Топ-папки</span>
                {ownership.data.data.owners.map((owner, index) => (
                  <Fragment key={`${owner.login}-${index}`}>
                    <span>{owner.login || "—"}</span>
                    <span>{owner.commits}</span>
                    <span>{owner.filesTouched}</span>
                    <span className="muted">{owner.topDirectories.join(", ") || "—"}</span>
                  </Fragment>
                ))}
              </div>
            </>
          ) : (
            <span className="muted">{ownership.data?.reason || "Нет данных о владельцах."}</span>
          )}
        </Card>

        <div className="grid-2">
          <ReviewInsightsCard repositoryId={data.sourceCraftId} />
          <PublicBadgeCard repositoryId={data.sourceCraftId} />
        </div>

        <div className="grid-2">
          <Card className="card stack">
            <div className="row" style={{ justifyContent: "space-between", alignItems: "center" }}>
              <div style={{ fontWeight: 600 }}>Рекомендации</div>
              <Button
                size="small"
                appearance="subtle"
                icon={analyze.isPending ? <Spinner size="tiny" /> : <ArrowSync24Regular />}
                disabled={analyze.isPending}
                onClick={runAnalyze}
              >
                {analyze.isPending ? "Анализируем…" : "Анализировать"}
              </Button>
            </div>
            {data.recommendations.length === 0 ? <span className="muted">Рекомендаций нет.</span> : null}
            <Accordion multiple collapsible>
              {data.recommendations.map((recommendation, index) => (
                <AccordionItem
                  className={`rec rec--${recommendation.priority}`}
                  value={`recommendation-${index}`}
                  key={index}
                >
                  <AccordionHeader>
                    <div className="row" style={{ justifyContent: "space-between", width: "100%", gap: "0.75rem" }}>
                      <strong>{recommendation.title}</strong>
                      <span className={`pill pill--${priorityTone(recommendation.priority)}`}>
                        {priorityLabels[recommendation.priority]}
                      </span>
                    </div>
                  </AccordionHeader>
                  <AccordionPanel>
                    <div className="stack" style={{ gap: "0.4rem" }}>
                      <RecommendationDetail label="Проблема" value={recommendation.problem} />
                      <RecommendationDetail label="Почему важно" value={recommendation.whyImportant} />
                      <RecommendationDetail label="Факты" value={recommendation.evidence} />
                      <RecommendationDetail label="Действие" value={recommendation.action} />
                      <RecommendationDetail label="Ожидаемое влияние" value={formatExpectedImpact(recommendation.expectedImpact)} />
                      {recommendation.sourceReference
                        ? <RecommendationDetail label="Источник" value={recommendation.sourceReference} />
                        : null}
                    </div>
                  </AccordionPanel>
                </AccordionItem>
              ))}
            </Accordion>
          </Card>

          <div className="stack">
            <Card className="card stack">
              <div style={{ fontWeight: 600 }}>Сильные и слабые стороны</div>
              <div>
                <div className="muted">Сильные стороны</div>
                <div className="row">
                  {data.strengths.length === 0 ? <span className="muted">—</span> : null}
                  {data.strengths.map((item) => (
                    <span key={item.category} className={`pill pill--${scoreTone(item.score)}`}>
                      {categoryLabels[item.category]} {item.score}
                    </span>
                  ))}
                </div>
              </div>
              <div>
                <div className="muted">Слабые стороны</div>
                <div className="row">
                  {data.weaknesses.length === 0 ? <span className="muted">—</span> : null}
                  {data.weaknesses.map((item) => (
                    <span key={item.category} className={`pill pill--${scoreTone(item.score)}`}>
                      {categoryLabels[item.category]} {item.score}
                    </span>
                  ))}
                </div>
              </div>
            </Card>

            <Card className="card stack">
              <div style={{ fontWeight: 600 }}>Структура</div>
              {structure.data?.data ? (
                <>
                  <div className="metric-row">
                    <span>Всего файлов</span>
                    <strong>{structure.data.data.totalFiles}</strong>
                  </div>
                  <div className="metric-row">
                    <span>Каталогов</span>
                    <strong>{structure.data.data.totalDirectories}</strong>
                  </div>
                  <div className="metric-row">
                    <span>Максимальная глубина</span>
                    <strong>{structure.data.data.maxDepth}</strong>
                  </div>
                  <div className="metric-row">
                    <span>Файлов в корне</span>
                    <strong>{structure.data.data.rootFiles}</strong>
                  </div>
                  <div className="metric-row">
                    <span>Крупнейшая папка</span>
                    <strong>{structure.data.data.largestDirectory || "—"}</strong>
                  </div>
                  <div className="metric-row">
                    <span>Файлов в крупнейшей папке</span>
                    <strong>{structure.data.data.largestDirectoryFiles}</strong>
                  </div>
                </>
              ) : structure.isError ? (
                <span className="muted">Не удалось загрузить структуру репозитория.</span>
              ) : (
                <span className="muted">Нет данных о структуре.</span>
              )}
            </Card>
          </div>
        </div>

        <Card className="card stack">
          <div style={{ fontWeight: 600, display: "flex", alignItems: "center", gap: "0.4rem" }}>
            <Sparkle24Regular /> AI-разбор (нужны вход и настройка провайдера в настройках)
          </div>
          <div className="row">
            {insightKinds.map((kind) => (
              <Button
                key={kind}
                appearance="subtle"
                disabled={aiRunning}
                onClick={() => runAi(kind)}
              >
                {insightLabels[kind]}
              </Button>
            ))}
          </div>
          {aiError ? <span className="tone-warn">{aiError}</span> : null}
          {aiThinking ? (
            <span className="thinking">
              Думает<span className="thinking__dots" />
            </span>
          ) : null}
          {aiReasoning ? (
            <details className="reasoning" open={!aiText}>
              <summary>Размышления модели</summary>
              <div className="reasoning__text">{aiReasoning}</div>
            </details>
          ) : null}
          {aiText ? <Markdown content={aiText} /> : null}
        </Card>

        <span className="muted">
          Хотите проанализировать свой репозиторий? <Link to="/repositories">Мои репозитории</Link>
        </span>
      </div>
    );
  };

  return (
    <>
      {statusOpen ? statusPanel : null}
      {renderContent()}
    </>
  );
}

function RecommendationDetail({ label, value }: { label: string; value: string }) {
  return (
    <div className="stack" style={{ gap: "0.1rem" }}>
      <span className="muted" style={{ fontSize: "0.8rem" }}>{label}</span>
      <span>{value}</span>
    </div>
  );
}
