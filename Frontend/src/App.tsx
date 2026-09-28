import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "./app/AppShell";
import { HomePage } from "./pages/HomePage";
import { RepositoriesPage } from "./pages/RepositoriesPage";
import { DashboardPage } from "./pages/DashboardPage";
import { MethodologyPage } from "./pages/MethodologyPage";
import { ComparePage } from "./pages/ComparePage";
import { SettingsPage } from "./pages/SettingsPage";

export function App() {
  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/rating" element={<Navigate to="/" replace />} />
        <Route path="/repositories" element={<RepositoriesPage />} />
        <Route path="/repositories/:id" element={<DashboardPage />} />
        <Route path="/compare" element={<ComparePage />} />
        <Route path="/cabinet" element={<Navigate to="/repositories" replace />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/methodology" element={<MethodologyPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppShell>
  );
}
