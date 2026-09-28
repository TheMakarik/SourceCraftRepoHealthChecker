import { Navigate, Route, Routes } from "react-router-dom";
import { Spinner } from "@fluentui/react-components";
import { AppShell } from "./app/AppShell";
import { LoginGate } from "./app/LoginGate";
import { HomePage } from "./pages/HomePage";
import { RepositoriesPage } from "./pages/RepositoriesPage";
import { DashboardPage } from "./pages/DashboardPage";
import { MethodologyPage } from "./pages/MethodologyPage";
import { SettingsPage } from "./pages/SettingsPage";
import { useMe } from "./shared/api/hooks";

export function App() {
  const me = useMe();

  if (me.isLoading)
    return (
      <div className="login-gate">
        <Spinner label="Проверяем сессию…" />
      </div>
    );

  if (me.isError)
    return <LoginGate />;

  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/rating" element={<Navigate to="/" replace />} />
        <Route path="/repositories" element={<RepositoriesPage />} />
        <Route path="/repositories/:id" element={<DashboardPage />} />
        <Route path="/cabinet" element={<Navigate to="/repositories" replace />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/methodology" element={<MethodologyPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppShell>
  );
}
