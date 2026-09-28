import { useEffect, useState } from "react";
import { appConfig } from "../config";

const { minSupportedWidthPx: minWidth, minSupportedHeightPx: minHeight } = appConfig;

export function useMinScreen(): boolean {
  const [tooSmall, setTooSmall] = useState(() => window.innerWidth < minWidth || window.innerHeight < minHeight);

  useEffect(() => {
    const onResize = () => setTooSmall(window.innerWidth < minWidth || window.innerHeight < minHeight);
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);

  return tooSmall;
}
