import type { ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";
import { Book24Regular, BranchCompare24Regular, Database24Regular, Home24Regular, Settings24Regular } from "@fluentui/react-icons";
import { useMinScreen } from "../shared/ui/useMinScreen";
import { ProfileMenu } from "../widgets/ProfileMenu";
import { appConfig } from "../shared/config";

function titleFor(pathname: string): string {
  if (pathname.startsWith("/repositories/"))
    return "Анализ репозитория";
  if (pathname.startsWith("/repositories"))
    return "Мои репозитории";
  if (pathname.startsWith("/compare"))
    return "Сравнение репозиториев";
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
            Требуется минимум {appConfig.minSupportedWidthRem} rem (примерно {appConfig.minSupportedWidthPx} px)
            ширины.
          </p>
        </div>
      </div>
    );
  }

  const isHome = location.pathname === "/" || location.pathname.startsWith("/rating");
  const isRepositories = location.pathname.startsWith("/repositories");
  const isCompare = location.pathname.startsWith("/compare");
  const isSettings = location.pathname.startsWith("/settings");
  const isMethodology = location.pathname.startsWith("/methodology");

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
          <Link
            className={`app__nav-link${isCompare ? " app__nav-link--active" : ""}`}
            to="/compare"
            title="Сравнение"
          >
            <BranchCompare24Regular />
          </Link>
          <Link className={`app__nav-link${isSettings ? " app__nav-link--active" : ""}`} to="/settings" title="Настройки">
            <Settings24Regular />
          </Link>
        </nav>
        <nav className="app__nav app__nav--bottom">
          <Link
            className={`app__nav-link${isMethodology ? " app__nav-link--active" : ""}`}
            to="/methodology"
            title="Мануал: как считается Score"
          >
            <Book24Regular />
          </Link>
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
