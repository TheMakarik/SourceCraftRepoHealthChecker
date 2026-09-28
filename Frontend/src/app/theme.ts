import { createLightTheme, type BrandVariants, type Theme } from "@fluentui/react-components";

const blackBrand: BrandVariants = {
  10: "#050505",
  20: "#111111",
  30: "#1a1a1a",
  40: "#222222",
  50: "#2b2b2b",
  60: "#333333",
  70: "#3d3d3d",
  80: "#4a4a4a",
  90: "#575757",
  100: "#666666",
  110: "#767676",
  120: "#8a8a8a",
  130: "#a0a0a0",
  140: "#bcbcbc",
  150: "#d6d6d6",
  160: "#f5f5f5"
};

export const srhcTheme: Theme = createLightTheme(blackBrand);
