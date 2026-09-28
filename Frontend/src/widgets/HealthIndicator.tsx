import { Tooltip } from "@fluentui/react-components";
import { useHealth } from "../shared/api/hooks";

export function HealthIndicator() {
  const health = useHealth();
  const healthy = health.isSuccess && health.data?.status?.toLowerCase() === "ok";
  const label = health.isPending ? "Проверяем backend…" : healthy ? "Backend доступен" : "Backend недоступен";

  return (
    <Tooltip content={label} relationship="label">
      <span className="health-indicator" role="status" aria-label={label}>
        <span className={`health-dot${healthy ? " health-dot--up" : " health-dot--down"}`} />
        <span className="muted health-indicator__text">Backend</span>
      </span>
    </Tooltip>
  );
}
