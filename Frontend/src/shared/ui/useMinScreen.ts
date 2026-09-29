import { useEffect, useState } from "react";
import { compactScreenMediaQuery } from "../config";

export function useMinScreen(): boolean {
  const [tooSmall, setTooSmall] = useState(() => window.matchMedia(compactScreenMediaQuery).matches);

  useEffect(() => {
    const mediaQuery = window.matchMedia(compactScreenMediaQuery);
    const update = () => setTooSmall(mediaQuery.matches);
    update();
    mediaQuery.addEventListener("change", update);
    return () => mediaQuery.removeEventListener("change", update);
  }, []);

  return tooSmall;
}
