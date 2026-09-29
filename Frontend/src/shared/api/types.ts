export type DataStatus = "NoData" | "Available" | "Unavailable";

export type ScoreCategory =
  | "Security"
  | "CodeHealth"
  | "Activity"
  | "Documentation"
  | "CiCd"
  | "Issues";

export type Priority = "Critical" | "High" | "Medium" | "Low";

export type AiProvider =
  | "OpenAI"
  | "Anthropic"
  | "GoogleGemini"
  | "Yandex"
  | "DeepSeek";

export type AiInsightKind =
  | "Recommendations"
  | "Explanation"
  | "ActionPlan"
  | "SecurityTriage"
  | "RiskForecast";

export type ReportFormat = "markdown" | "json" | "html" | "pdf";

export interface CategoryScore {
  category: ScoreCategory;
  score: number;
  dataStatus: DataStatus;
}

export interface MetricScore {
  code: string;
  rawValue: number;
  normalizedScore: number;
  weight: number;
  dataStatus: DataStatus;
}

export interface Finding {
  kind: string;
  severity: string;
  status: string;
  title: string;
  package?: string | null;
  filePath?: string | null;
  cvssScore?: number | null;
}

export interface Recommendation {
  priority: Priority;
  title: string;
  problem: string;
  whyImportant: string;
  evidence: string;
  action: string;
  expectedImpact: string;
  sourceReference: string;
}

export interface Analysis {
  sourceCraftId: string;
  name: string;
  fullName: string;
  url: string;
  language: string;
  isPrivate: boolean;
  ownerUserId?: string | null;
  likesCount: number;
  score: number;
  analyzedAt?: string | null;
  categories: CategoryScore[];
  metrics: MetricScore[];
  strengths: CategoryScore[];
  weaknesses: CategoryScore[];
  recommendations: Recommendation[];
  findings: Finding[];
}

export interface RepositoryComparisonItem {
  sourceCraftId: string;
  name: string;
  fullName: string;
  url: string;
  language: string;
  likesCount: number;
  lastActivityAt: string;
  score: number | null;
  analyzedAt?: string | null;
  categories: CategoryScore[];
  metrics: MetricScore[];
  strengths: CategoryScore[];
  weaknesses: CategoryScore[];
}

export interface RepositoryComparison {
  items: RepositoryComparisonItem[];
}

export interface LeaderboardItem {
  place: number;
  sourceCraftId: string;
  name: string;
  fullName: string;
  url: string;
  score: number | null;
  likesCount: number;
  language: string;
  lastActivityAt: string;
  analyzedAt?: string | null;
}

export interface LeaderboardPage {
  items: LeaderboardItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type IntegrityStatus = "ok" | "check" | "suspicious";

export interface RepositoryIntegrity {
  status: IntegrityStatus;
  score: number;
  signals: string[];
}

export interface SourceCraftResult<T> {
  status: DataStatus;
  data: T | null;
  reason?: string | null;
}

export interface StructureReport {
  totalFiles: number;
  totalDirectories: number;
  maxDepth: number;
  rootFiles: number;
  largestDirectory: string;
  largestDirectoryFiles: number;
}

export interface SourceCraftUser {
  id: string;
  login: string;
  displayName: string;
  email?: string | null;
  avatarUrl?: string | null;
}

export interface TokenOverview {
  sourceCraftTokenPrefix: string | null;
  aiTokenPrefix: string | null;
}

export interface UserRepository {
  id: string;
  name: string;
  fullName: string;
  url: string;
  language: string;
  likesCount: number;
  lastActivityAt: string;
  isPrivate: boolean;
  defaultBranch: string;
}

export interface AiSettings {
  provider: AiProvider;
  baseUrl: string | null;
  model: string;
}

export interface AiProviderModels {
  provider: AiProvider;
  models: string[];
}

export interface AiTestResult {
  ok: boolean;
  message: string;
}

export interface HistoryPoint {
  analyzedAt: string;
  score: number;
}

export interface AiSummaryResult {
  summary: string;
  provider: AiProvider;
  model: string;
}

export interface AiInsightResult {
  kind: AiInsightKind;
  content: string;
  provider: AiProvider;
  model: string;
}

export type RepositoryNodeType = "file" | "directory";

export interface RepositoryTreeEntry {
  name: string;
  path: string;
  type: RepositoryNodeType;
}

export interface RepositoryTreeSelection {
  path: string;
  type: RepositoryNodeType;
}

export interface RepositoryTree {
  truncated: boolean;
  entries: RepositoryTreeEntry[];
}

export interface RepositoryFile {
  path: string;
  content: string;
  language: string;
  truncated: boolean;
  binary: boolean;
}

export interface FolderReport {
  path: string;
  files: number;
  todoCount: number;
  fixmeCount: number;
  readme: boolean;
  license: boolean;
  tests: boolean;
}

export interface OwnerStat {
  login: string;
  commits: number;
  filesTouched: number;
  topDirectories: string[];
}

export interface RepositoryOwnership {
  owners: OwnerStat[];
  busFactor: number;
  totalContributors: number;
}

export type AnalysisStatusValue = "queued" | "running" | "completed" | "failed";

export interface AnalysisStatus {
  repositoryId: string;
  status: AnalysisStatusValue;
  score: number | null;
  updatedAt: string | null;
}

export interface AnalysisSnapshotMessage {
  type: "snapshot";
  items: AnalysisStatus[];
}

export interface AnalysisStatusMessage extends AnalysisStatus {
  type: "status";
}

export type AnalysisSocketMessage = AnalysisSnapshotMessage | AnalysisStatusMessage;

export interface AiProviderToken {
  provider: AiProvider;
  tokenPrefix: string | null;
}
