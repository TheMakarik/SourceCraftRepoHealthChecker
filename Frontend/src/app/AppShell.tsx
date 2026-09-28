import type { ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";
import { Database24Regular, Home24Regular, Settings24Regular } from "@fluentui/react-icons";
import { useMinScreen } from "../shared/ui/useMinScreen";
import { ProfileMenu } from "../widgets/ProfileMenu";
import { appConfig } from "../shared/config";

function titleFor(pathname: string): string {
  if (pathname.startsWith("/repositories/"))
    return "Анализ репозитория";
  if (pathname.startsWith("/repositories"))
    return "Мои репозитории";
  if (pathname.startsWith("/settings"))
    return "Настройки";
  if (pathname.startsWith("/methodology"))
    return "Методика Repo Health Score";
  return "Рейтинг";
}

export function AppShell({ children }: { children: ReactNode }) {
  const location = useLocation();
  const tooSmall = useMinScreen();

  if (tooSmall) {
    return (
      <div className="small-screen">
        <div>
          <h2>Экран слишком маленький</h2>
          <p className="muted">
            Ваш размер экрана слишком маленький для работы с SourceCraft Repo Health Checker.
            <br />
            Требуется минимум {appConfig.minSupportedWidthRem}×{appConfig.minSupportedHeightRem} rem (примерно{" "}
            {appConfig.minSupportedWidthPx}×{appConfig.minSupportedHeightPx} px).
          </p>
        </div>
      </div>
    );
  }

  const isHome = location.pathname === "/" || location.pathname.startsWith("/rating");
  const isRepositories = location.pathname.startsWith("/repositories");
  const isSettings = location.pathname.startsWith("/settings");

  return (
    <div className="app">
      <aside className="app__sidebar">
        <img className="app__logo" src="/assets/sourcecraft-icon.svg" alt="SourceCraft" />
        <nav className="app__nav">
          <Link className={`app__nav-link${isHome ? " app__nav-link--active" : ""}`} to="/" title="Рейтинг">
            <Home24Regular />
          </Link>
          <Link
            className={`app__nav-link${isRepositories ? " app__nav-link--active" : ""}`}
            to="/repositories"
            title="Репозитории"
          >
            <Database24Regular />
          </Link>
          <Link className={`app__nav-link${isSettings ? " app__nav-link--active" : ""}`} to="/settings" title="Настройки">
            <Settings24Regular />
          </Link>
        </nav>
        <nav className="app__nav app__nav--bottom">
          <ProfileMenu />
        </nav>
      </aside>
      <div className="app__main">
        <header className="app__topbar">
          <div className="app__title">
            <Home24Regular />
            {titleFor(location.pathname)}
          </div>
        </header>
        <main className="app__content">{children}</main>
      </div>
    </div>
  );
}
