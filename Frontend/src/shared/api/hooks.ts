import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type { AiInsightKind, AiProvider, RepositoryIntegrity, RepositoryOwnership, SourceCraftResult } from "./types";

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");

const fetchOwnership = async (id: string): Promise<SourceCraftResult<RepositoryOwnership>> => {
  const response = await fetch(`${apiBaseUrl}/api/repositories/${encodeURIComponent(id)}/ownership`, {
    credentials: "include"
  });
  if (!response.ok)
    throw new Error(`HTTP ${response.status}`);
  return (await response.json()) as SourceCraftResult<RepositoryOwnership>;
};

const fetchIntegrity = async (id: string): Promise<RepositoryIntegrity> => {
  const response = await fetch(`${apiBaseUrl}/api/repositories/${encodeURIComponent(id)}/integrity`, {
    credentials: "include"
  });
  if (!response.ok)
    throw new Error(`HTTP ${response.status}`);
  return (await response.json()) as RepositoryIntegrity;
};

export interface LeaderboardQuery {
  language?: string;
  sort?: string;
  page?: number;
  pageSize?: number;
}

export const useLeaderboard = (query: LeaderboardQuery) =>
  useQuery({ queryKey: ["leaderboard", query], queryFn: () => api.leaderboard(query) });

export const useLanguages = () =>
  useQuery({ queryKey: ["languages"], queryFn: api.languages, staleTime: 5 * 60_000 });

export const useAnalysis = (id: string | undefined) =>
  useQuery({
    queryKey: ["analysis", id],
    queryFn: () => api.analysis(id as string),
    enabled: Boolean(id),
    retry: false
  });

export const useComparison = (ids: string[]) =>
  useQuery({
    queryKey: ["comparison", ids],
    queryFn: () => api.compare(ids),
    enabled: ids.length >= 2 && ids.length <= 4,
    retry: false
  });

export const useStructure = (id: string | undefined) =>
  useQuery({
    queryKey: ["structure", id],
    queryFn: () => api.structure(id as string),
    enabled: Boolean(id),
    retry: false
  });

export const useHistory = (id: string | undefined) =>
  useQuery({
    queryKey: ["history", id],
    queryFn: () => api.history(id as string),
    enabled: Boolean(id),
    retry: false
  });

export const useTree = (id: string | undefined, path: string, enabled: boolean) =>
  useQuery({
    queryKey: ["tree", id, path],
    queryFn: () => api.tree(id as string, { path }),
    enabled: Boolean(id) && enabled,
    retry: false,
    staleTime: 5 * 60_000
  });

export const useRepositoryFile = (id: string | undefined, path: string | undefined) =>
  useQuery({
    queryKey: ["file", id, path],
    queryFn: () => api.file(id as string, path as string),
    enabled: Boolean(id) && Boolean(path),
    retry: false,
    staleTime: 5 * 60_000
  });

export const useFolders = (id: string | undefined) =>
  useQuery({
    queryKey: ["folders", id],
    queryFn: () => api.folders(id as string),
    enabled: Boolean(id),
    retry: false
  });

export const useOwnership = (id: string | undefined) =>
  useQuery({
    queryKey: ["ownership", id],
    queryFn: () => fetchOwnership(id as string),
    enabled: Boolean(id),
    retry: false,
    staleTime: 5 * 60_000
  });

export const useRepositoryIntegrity = (id: string | undefined) =>
  useQuery({
    queryKey: ["integrity", id],
    queryFn: () => fetchIntegrity(id as string),
    enabled: Boolean(id),
    retry: false,
    staleTime: 5 * 60_000
  });

export const useHealth = () =>
  useQuery({
    queryKey: ["health"],
    queryFn: api.health,
    retry: false,
    refetchInterval: 30_000,
    staleTime: 15_000
  });

export const useMe = () =>
  useQuery({ queryKey: ["me"], queryFn: api.me, retry: false, staleTime: 30_000 });

export const useMeTokens = (enabled: boolean) =>
  useQuery({ queryKey: ["me-tokens"], queryFn: api.meTokens, enabled, retry: false, staleTime: 15_000 });

export const useMeAi = (enabled: boolean) =>
  useQuery({ queryKey: ["me-ai"], queryFn: api.getMeAi, enabled, retry: false, staleTime: 30_000 });

export const useAiModels = () =>
  useQuery({ queryKey: ["ai-models"], queryFn: api.getAiModels, retry: false, staleTime: 60 * 60_000 });

export const useMyRepositories = (enabled: boolean) =>
  useQuery({ queryKey: ["my-repositories"], queryFn: api.myRepositories, enabled, retry: false });

export const useRefresh = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.refresh(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["leaderboard"] })
  });
};

export const useAnalyze = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.analyze(id),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: ["analysis", id] });
      queryClient.invalidateQueries({ queryKey: ["leaderboard"] });
      queryClient.invalidateQueries({ queryKey: ["structure", id] });
      queryClient.invalidateQueries({ queryKey: ["history", id] });
      queryClient.invalidateQueries({ queryKey: ["folders", id] });
      queryClient.invalidateQueries({ queryKey: ["tree", id] });
      queryClient.invalidateQueries({ queryKey: ["file", id] });
      queryClient.invalidateQueries({ queryKey: ["ownership", id] });
    }
  });
};

export const useStoreSourceCraftToken = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (token: string) => api.storeSourceCraftToken(token),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["my-repositories"] });
      queryClient.invalidateQueries({ queryKey: ["me-tokens"] });
    }
  });
};

export const useStoreAi = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (settings: { provider: AiProvider; baseUrl?: string | null; model: string; token?: string | null }) =>
      api.storeAi(settings),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["me-ai"] });
      queryClient.invalidateQueries({ queryKey: ["me-tokens"] });
    }
  });
};

export const useTestAi = () => useMutation({ mutationFn: () => api.testAi() });

export const useLogout = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.logout(),
    onSuccess: () => {
      queryClient.clear();
      window.location.href = "/";
    }
  });
};

export const useAiSummary = () => useMutation({ mutationFn: (id: string) => api.aiSummary(id) });

export const useAiInsight = () =>
  useMutation({ mutationFn: (input: { id: string; kind: AiInsightKind }) => api.aiInsight(input.id, input.kind) });
