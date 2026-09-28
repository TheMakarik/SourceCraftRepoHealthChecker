import type {
  AiInsightKind,
  AiInsightResult,
  AiProvider,
  AiProviderModels,
  AiSettings,
  AiSummaryResult,
  AiTestResult,
  Analysis,
  FolderReport,
  HistoryPoint,
  LeaderboardPage,
  ReportFormat,
  RepositoryFile,
  RepositoryTree,
  SourceCraftResult,
  SourceCraftUser,
  StructureReport,
  TokenOverview,
  UserRepository
} from "./types";

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");

export class ApiError extends Error {
  public constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
    this.name = "ApiError";
  }
}

async function send(path: string, init?: RequestInit): Promise<Response> {
  const response = await fetch(`${baseUrl}${path}`, {
    credentials: "include",
    ...init,
    headers: {
      ...(init?.body ? { "Content-Type": "application/json" } : {}),
      ...init?.headers
    }
  });

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    throw new ApiError(response.status, text || `HTTP ${response.status}`);
  }

  return response;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await send(path, init);

  if (response.status === 204)
    return undefined as T;

  return (await response.json()) as T;
}

async function requestOrNull<T>(path: string, init?: RequestInit): Promise<T | null> {
  const response = await send(path, init);

  if (response.status === 204)
    return null;

  return (await response.json()) as T;
}

export const api = {
  health: () => request<{ status: string }>("/healthz"),

  leaderboard: (options: { language?: string; sort?: string; page?: number; pageSize?: number } = {}) => {
    const query = new URLSearchParams();
    if (options.language) query.set("language", options.language);
    if (options.sort) query.set("sort", options.sort);
    if (options.page) query.set("page", String(options.page));
    if (options.pageSize) query.set("pageSize", String(options.pageSize));
    const suffix = query.size > 0 ? `?${query.toString()}` : "";
    return request<LeaderboardPage>(`/api/repositories${suffix}`);
  },

  refresh: () => request<{ refreshed: number }>("/api/repositories/refresh", { method: "POST" }),

  languages: () => request<string[]>("/api/repositories/languages"),

  analysis: (id: string) => request<Analysis>(`/api/repositories/${encodeURIComponent(id)}/analysis`),

  history: (id: string) => request<HistoryPoint[]>(`/api/repositories/${encodeURIComponent(id)}/history`),

  structure: (id: string) =>
    request<SourceCraftResult<StructureReport>>(`/api/repositories/${encodeURIComponent(id)}/structure`),

  tree: (id: string, options: { path?: string; recursive?: boolean } = {}) => {
    const query = new URLSearchParams();
    if (options.path) query.set("path", options.path);
    query.set("recursive", String(options.recursive ?? false));
    return request<RepositoryTree>(`/api/repositories/${encodeURIComponent(id)}/tree?${query.toString()}`);
  },

  file: (id: string, path: string) =>
    request<RepositoryFile>(`/api/repositories/${encodeURIComponent(id)}/file?path=${encodeURIComponent(path)}`),

  folders: (id: string) =>
    request<FolderReport[]>(`/api/repositories/${encodeURIComponent(id)}/folders`),

  reportUrl: (id: string, format: ReportFormat) =>
    `${baseUrl}/api/repositories/${encodeURIComponent(id)}/report?format=${format}`,

  reportMarkdownUrl: (id: string) => `${baseUrl}/api/repositories/${encodeURIComponent(id)}/report.md`,

  me: () => request<SourceCraftUser>("/api/me"),

  meTokens: () => request<TokenOverview>("/api/me/tokens"),

  logout: () => request<void>("/auth/logout", { method: "POST" }),

  getMeAi: () => requestOrNull<AiSettings>("/api/me/ai"),

  getAiModels: () => request<AiProviderModels[]>("/api/me/ai/models"),

  testAi: () => request<AiTestResult>("/api/me/ai/test", { method: "POST" }),

  storeSourceCraftToken: (token: string) =>
    request<void>("/api/me/sourcecraft-token", { method: "POST", body: JSON.stringify({ token }) }),

  myRepositories: () => request<UserRepository[]>("/api/me/repositories"),

  analyze: (id: string) =>
    request<unknown>(`/api/me/repositories/${encodeURIComponent(id)}/analyze`, { method: "POST" }),

  storeAi: (settings: { provider: AiProvider; baseUrl?: string | null; model: string; token?: string | null }) =>
    request<void>("/api/me/ai", { method: "PUT", body: JSON.stringify(settings) }),

  aiSummary: (id: string) =>
    request<AiSummaryResult>(`/api/repositories/${encodeURIComponent(id)}/ai-summary`, { method: "POST" }),

  aiInsight: (id: string, kind: AiInsightKind) =>
    request<AiInsightResult>(`/api/repositories/${encodeURIComponent(id)}/ai-insights/${kind}`, { method: "POST" })
};

export const authLoginUrl = `${baseUrl}/auth/login`;
