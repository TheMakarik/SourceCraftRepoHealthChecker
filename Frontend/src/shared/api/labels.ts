import type { AiInsightKind, AiProvider, CategoryScore, DataStatus, Priority, ScoreCategory } from "./types";

export const categoryLabels: Record<ScoreCategory, string> = {
  Security: "Security",
  CodeHealth: "Code health",
  Activity: "Активность",
  Documentation: "Документация",
  CiCd: "CI/CD",
  Issues: "Issues"
};

export const categoryOrder: ScoreCategory[] = [
  "Security",
  "CodeHealth",
  "Activity",
  "Documentation",
  "CiCd",
  "Issues"
];

export const priorityLabels: Record<Priority, string> = {
  Critical: "Критичный",
  High: "Высокий",
  Medium: "Средний",
  Low: "Низкий"
};

export const providerLabels: Record<AiProvider, string> = {
  OpenAI: "OpenAI",
  Anthropic: "Anthropic",
  GoogleGemini: "Google Gemini",
  Yandex: "Yandex",
  DeepSeek: "DeepSeek"
};

export const insightLabels: Record<AiInsightKind, string> = {
  Recommendations: "Рекомендации",
  Explanation: "Объяснение",
  ActionPlan: "План улучшения",
  SecurityTriage: "Разбор безопасности",
  RiskForecast: "Прогноз рисков"
};

const languageAliases: Record<string, string> = {
  "F*": "F#"
};

export function languageDisplayName(language?: string | null): string {
  if (!language)
    return "—";
  const trimmed = language.trim();
  return languageAliases[trimmed] ?? trimmed;
}

export type ScoreTone = "good" | "warn" | "bad";

export function scoreTone(score: number): ScoreTone {
  if (score >= 70)
    return "good";
  if (score >= 40)
    return "warn";
  return "bad";
}

export function toneColor(tone: ScoreTone): string {
  switch (tone) {
    case "good":
      return "var(--srhc-good)";
    case "warn":
      return "var(--srhc-warn)";
    default:
      return "var(--srhc-bad)";
  }
}

export function scoreGrade(score: number): string {
  if (score >= 85)
    return "ИДЕАЛЬНО";
  if (score >= 70)
    return "ХОРОШО";
  if (score >= 40)
    return "ТЕРПИМО";
  return "ОПАСНО";
}

export function severityTone(severity: string): ScoreTone {
  const normalized = severity.toLowerCase();
  if (normalized === "critical" || normalized === "high")
    return "bad";
  if (normalized === "medium")
    return "warn";
  return "good";
}

export function priorityTone(priority: Priority): ScoreTone {
  return priority === "Critical" || priority === "High" ? "bad" : priority === "Medium" ? "warn" : "good";
}

export function formatDate(value?: string | null): string {
  if (!value)
    return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime()) || date.getFullYear() < 2000)
    return "—";
  return date.toLocaleString("ru-RU", { day: "numeric", month: "long", hour: "2-digit", minute: "2-digit" });
}

export function formatDateShort(value?: string | null): string {
  if (!value)
    return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime()) || date.getFullYear() < 2000)
    return "—";
  return date.toLocaleDateString("ru-RU", { day: "2-digit", month: "2-digit", year: "numeric" });
}

export function dataStatusLabel(status: DataStatus): string {
  switch (status) {
    case "Available":
      return "Данные есть";
    case "NoData":
      return "Нет данных";
    case "Unavailable":
      return "Источник недоступен";
    default:
      return "Нет данных";
  }
}

export function hasAvailableCategories(categories: CategoryScore[]): boolean {
  return categories.some((category) => category.dataStatus === "Available");
}
