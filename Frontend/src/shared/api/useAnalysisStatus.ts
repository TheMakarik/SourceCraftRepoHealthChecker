import { useCallback, useSyncExternalStore } from "react";
import { appConfig } from "../config";
import type { AnalysisSocketMessage, AnalysisStatus, AnalysisStatusValue } from "./types";

type Listener = () => void;

const listeners = new Set<Listener>();
let statuses: ReadonlyMap<string, AnalysisStatus> = new Map();
let socket: WebSocket | null = null;
let reconnectTimer: number | null = null;
let reconnectAttempts = 0;
let started = false;

function emit() {
  listeners.forEach((listener) => listener());
}

function replaceStatuses(items: AnalysisStatus[]) {
  statuses = new Map(items.map((item) => [item.repositoryId, item]));
  emit();
}

function upsertStatus(item: AnalysisStatus) {
  const next = new Map(statuses);
  next.set(item.repositoryId, item);
  statuses = next;
  emit();
}

function isStatusValue(value: unknown): value is AnalysisStatusValue {
  return value === "queued" || value === "running" || value === "completed" || value === "failed";
}

function normalizeStatus(value: unknown): AnalysisStatus | null {
  if (typeof value !== "object" || value === null)
    return null;
  const record = value as Record<string, unknown>;
  if (typeof record.repositoryId !== "string" || !isStatusValue(record.status))
    return null;
  return {
    repositoryId: record.repositoryId,
    status: record.status,
    score: typeof record.score === "number" ? record.score : null,
    updatedAt: typeof record.updatedAt === "string" ? record.updatedAt : null
  };
}

function handleMessage(raw: unknown) {
  if (typeof raw !== "string")
    return;
  let message: AnalysisSocketMessage;
  try {
    message = JSON.parse(raw) as AnalysisSocketMessage;
  } catch {
    return;
  }
  if (message.type === "snapshot" && Array.isArray(message.items)) {
    const items = message.items.map(normalizeStatus).filter((item): item is AnalysisStatus => item !== null);
    replaceStatuses(items);
  } else if (message.type === "status") {
    const item = normalizeStatus(message);
    if (item)
      upsertStatus(item);
  }
}

function buildWebSocketUrl(): string {
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  return `${protocol}//${window.location.host}/ws/analysis`;
}

function scheduleReconnect() {
  if (reconnectTimer !== null)
    return;
  const delay = Math.min(
    appConfig.wsReconnectBaseDelayMs * 2 ** reconnectAttempts,
    appConfig.wsReconnectMaxDelayMs
  );
  reconnectAttempts += 1;
  reconnectTimer = window.setTimeout(() => {
    reconnectTimer = null;
    connect();
  }, delay);
}

function connect() {
  if (socket && (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING))
    return;
  const nextSocket = new WebSocket(buildWebSocketUrl());
  socket = nextSocket;
  nextSocket.onopen = () => {
    reconnectAttempts = 0;
  };
  nextSocket.onmessage = (event) => handleMessage(event.data);
  nextSocket.onerror = () => nextSocket.close();
  nextSocket.onclose = () => {
    if (socket === nextSocket)
      socket = null;
    if (started)
      scheduleReconnect();
  };
}

function start() {
  if (started)
    return;
  started = true;
  connect();
}

function subscribe(listener: Listener): () => void {
  listeners.add(listener);
  start();
  return () => listeners.delete(listener);
}

function getSnapshot(): ReadonlyMap<string, AnalysisStatus> {
  return statuses;
}

export interface AnalysisStatusLookup {
  statusFor: (repositoryId: string | undefined) => AnalysisStatus | undefined;
}

export function useAnalysisStatus(): AnalysisStatusLookup {
  const snapshot = useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
  const statusFor = useCallback((repositoryId: string | undefined) => (repositoryId ? snapshot.get(repositoryId) : undefined), [snapshot]);
  return { statusFor };
}
