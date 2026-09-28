import { Spinner } from "@fluentui/react-components";

export function LoadingView({ label = "Загрузка…" }: { label?: string }) {
  return (
    <div className="card row">
      <Spinner size="small" />
      <span className="muted">{label}</span>
    </div>
  );
}

export function ErrorView({ message }: { message: string }) {
  return (
    <div className="card">
      <span className="tone-bad">{message}</span>
    </div>
  );
}
