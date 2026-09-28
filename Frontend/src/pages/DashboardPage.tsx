import { useEffect, useState } from "react";
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
import { useAiInsight, useAiSummary, useAnalysis, useAnalyze, useHistory, useStructure } from "../shared/api/hooks";
import { useAnalysisStatus } from "../shared/api/useAnalysisStatus";
import type { AiInsightKind } from "../shared/api/types";
import {
  categoryLabels,
  priorityLabels,
  priorityTone,
  formatDate,
  formatDateShort,
  scoreGrade,
  scoreTone
} from "../shared/api/labels";
import { ErrorView, LoadingView } from "../shared/ui/Status";
import { appConfig } from "../shared/config";
import { ScoreRing } from "../widgets/ScoreRing";
import { CategoryCapsules } from "../widgets/CategoryCapsules";
import { CategoryTabs } from "../widgets/CategoryTabs";
import { AnalysisStatusBadge } from "../widgets/AnalysisStatusBadge";
import { MetricsTable } from "../widgets/MetricsTable";
import { FoldersPanel } from "../widgets/FoldersPanel";
import { FileBrowser } from "../widgets/FileBrowser";
import { api, ApiError } from "../shared/api/client";
import { insightLabels } from "../shared/api/labels";

const insightKinds: AiInsightKind[] = ["Recommendations", "Explanation", "ActionPlan", "SecurityTriage", "RiskForecast"];

export function DashboardPage() {
  const { id } = useParams<{ id: string }>();
  const analysis = useAnalysis(id);
  const structure = useStructure(id);
  const history = useHistory(id);
  const analyze = useAnalyze();
  const summary = useAiSummary();
  const insight = useAiInsight();
  const { statusFor } = useAnalysisStatus();
  const [aiError, setAiError] = useState<string | null>(null);
  const [chartReady, setChartReady] = useState(false);

  useEffect(() => {
    setChartReady(false);
    const timer = window.setTimeout(() => setChartReady(true), appConfig.chartLoadDelayMs);
    return () => window.clearTimeout(timer);
  }, [id]);

  const runAnalyze = () => {
    if (!id)
      return;
    analyze.mutate(id);
  };

  const analyzeHint = analyze.error
    ? analyze.error instanceof ApiError && analyze.error.status === 401
      ? "Нужен вход и PAT — настройте в личном кабинете."
      : "Не удалось запустить анализ. Попробуйте позже."
    : null;

  if (analysis.isLoading)
    return <LoadingView label="Загружаем анализ…" />;

  if (analysis.isError) {
    const status = analysis.error instanceof ApiError ? analysis.error.status : undefined;
    if (status === 404)
      return (
        <div className="stack">
          <ErrorView message="Репозиторий ещё не проанализирован." />
          <div className="row">
            <Button
              appearance="primary"
              icon={analyze.isPending ? <Spinner size="tiny" /> : <ArrowSync24Regular />}
              disabled={analyze.isPending}
              onClick={runAnalyze}
            >
              Анализировать
            </Button>
            {analyzeHint ? <span className="tone-warn">{analyzeHint}</span> : null}
          </div>
        </div>
      );
    if (status === 401)
      return <ErrorView message="Нужен вход и PAT — настройте в личном кабинете." />;
    return <ErrorView message="Не удалось загрузить анализ. Попробуйте позже." />;
  }

  if (!analysis.data)
    return <ErrorView message="Не удалось загрузить анализ." />;

  const data = analysis.data;
  const historyPoints = history.data ?? [];
  const aiContent = summary.data?.summary ?? insight.data?.content;

  const runAi = (kind?: AiInsightKind) => {
    setAiError(null);
    if (!id)
      return;
    if (kind)
      insight.mutate(
        { id, kind },
        { onError: () => setAiError("Не удалось получить AI-разбор. Войдите и настройте провайдера в личном кабинете.") }
      );
    else
      summary.mutate(id, {
        onError: () => setAiError("Не удалось получить AI-резюме. Войдите и настройте провайдера в личном кабинете.")
      });
  };

  return (
    <div className="stack">
      <div className="grid-2">
        <Card className="card">
          <div className="row" style={{ gap: "1.5rem" }}>
            <ScoreRing score={data.score} grade={scoreGrade(data.score)} />
            <div className="stack" style={{ flex: 1 }}>
              <div>
                <div className="row" style={{ justifyContent: "space-between", gap: "0.75rem" }}>
                  <div style={{ fontSize: "1.1rem", fontWeight: 600 }}>{data.fullName}</div>
                  <AnalysisStatusBadge status={statusFor(id)} score={data.score} />
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
              <span className="muted">Последний анализ: {formatDate(data.analyzedAt)}</span>
              <div className="row">
                <Button
                  appearance="secondary"
                  icon={analyze.isPending ? <Spinner size="tiny" /> : <ArrowSync24Regular />}
                  disabled={analyze.isPending}
                  onClick={runAnalyze}
                >
                  Анализировать
                </Button>
                <Button
                  appearance="primary"
                  icon={summary.isPending ? <Spinner size="tiny" /> : <Sparkle24Regular />}
                  disabled={summary.isPending || insight.isPending}
                  onClick={() => runAi()}
                >
                  AI-резюме
                </Button>
                <Button as="a" href={api.reportUrl(data.sourceCraftId, "pdf")} icon={<ArrowDownload24Regular />}>
                  PDF
                </Button>
                <Button as="a" href={api.reportMarkdownUrl(data.sourceCraftId)} icon={<ArrowDownload24Regular />}>
                  Markdown
                </Button>
                <Button as="a" href={api.reportUrl(data.sourceCraftId, "json")} icon={<ArrowDownload24Regular />} appearance="subtle">
                  JSON
                </Button>
                <Button as="a" href={api.reportUrl(data.sourceCraftId, "html")} icon={<ArrowDownload24Regular />} appearance="subtle">
                  HTML
                </Button>
              </div>
              {analyzeHint ? <span className="tone-warn">{analyzeHint}</span> : null}
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
        ) : historyPoints.length >= 2 ? (
          <div style={{ width: "100%", height: "16rem" }}>
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={historyPoints} margin={{ top: 8, right: 16, bottom: 0, left: -16 }}>
                <XAxis dataKey="analyzedAt" tickFormatter={(value) => formatDateShort(String(value))} />
                <YAxis domain={[0, 100]} />
                <Tooltip labelFormatter={(value) => formatDate(String(value))} />
                <Line type="monotone" dataKey="score" name="Score" stroke="var(--srhc-good)" strokeWidth={2} />
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
        <div style={{ fontWeight: 600 }}>Папки</div>
        <FoldersPanel repositoryId={id} />
      </Card>

      <Card className="card stack">
        <div style={{ fontWeight: 600 }}>Файлы</div>
        <FileBrowser repositoryId={id} />
      </Card>

      <div className="grid-2">
        <Card className="card stack">
          <div style={{ fontWeight: 600 }}>Рекомендации</div>
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
                    <RecommendationDetail label="Ожидаемое влияние" value={recommendation.expectedImpact} />
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
              disabled={insight.isPending}
              onClick={() => runAi(kind)}
            >
              {insightLabels[kind]}
            </Button>
          ))}
        </div>
        {aiError ? <span className="tone-warn">{aiError}</span> : null}
        {aiContent ? <div className="markdown">{aiContent}</div> : null}
      </Card>

      <span className="muted">
        Хотите проанализировать свой репозиторий? <Link to="/repositories">Мои репозитории</Link>
      </span>
    </div>
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
