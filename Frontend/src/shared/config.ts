const rootFontSizePx = 16;
const minSupportedWidthPx = 320;

export const appConfig = {
  chartLoadDelayMs: 1000,
  leaderboardPageSize: 20,
  minSupportedWidthPx,
  minSupportedWidthRem: minSupportedWidthPx / rootFontSizePx,
  wsReconnectBaseDelayMs: 1000,
  wsReconnectMaxDelayMs: 15000,
  treeChildrenPageSize: 200,
  maxRenderedFileBytes: 262144
} as const;

export const compactScreenMediaQuery = `(max-width: ${minSupportedWidthPx - 0.02}px)`;
