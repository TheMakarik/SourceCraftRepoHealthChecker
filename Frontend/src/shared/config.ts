const rootFontSizePx = 16;
const minSupportedWidthPx = 368;
const minSupportedHeightPx = 480;

export const appConfig = {
  chartLoadDelayMs: 1000,
  leaderboardPageSize: 20,
  minSupportedWidthPx,
  minSupportedHeightPx,
  minSupportedWidthRem: minSupportedWidthPx / rootFontSizePx,
  minSupportedHeightRem: minSupportedHeightPx / rootFontSizePx,
  wsReconnectBaseDelayMs: 1000,
  wsReconnectMaxDelayMs: 15000,
  treeChildrenPageSize: 200,
  maxRenderedFileBytes: 262144
} as const;

export const compactScreenMediaQuery = `(max-width: ${minSupportedWidthPx - 0.02}px), (max-height: ${minSupportedHeightPx - 0.02}px)`;
