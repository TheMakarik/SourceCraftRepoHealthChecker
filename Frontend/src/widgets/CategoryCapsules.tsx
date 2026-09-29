import { PlugDisconnected16Regular, QuestionCircle16Regular } from "@fluentui/react-icons";
import type { CategoryScore, DataStatus } from "../shared/api/types";
import { categoryLabels, categoryOrder, dataStatusLabel, scoreTone, toneColor } from "../shared/api/labels";

function statusIcon(status: DataStatus) {
  return status === "Unavailable" ? <PlugDisconnected16Regular /> : <QuestionCircle16Regular />;
}

export function CategoryCapsules({ categories }: { categories: CategoryScore[] }) {
  const byCategory = new Map(categories.map((item) => [item.category, item]));

  return (
    <div className="capsules">
      {categoryOrder.map((category) => {
        const item = byCategory.get(category);
        const status: DataStatus = item?.dataStatus ?? "NoData";
        const score = item && item.dataStatus === "Available" ? item.score : null;
        const color = score === null ? "var(--srhc-border)" : toneColor(scoreTone(score));
        const label = dataStatusLabel(status);

        return (
          <div className="capsule" key={category} title={`${categoryLabels[category]}: ${label}`}>
            <div className="capsule__track">
              <div className="capsule__fill" style={{ height: `${score ?? 0}%`, background: color }}>
                <span className="capsule__value" style={{ color: score === null ? "var(--srhc-muted)" : color }}>
                  {score === null ? statusIcon(status) : score}
                </span>
              </div>
            </div>
            <span className="capsule__label">{categoryLabels[category]}</span>
            {score === null ? (
              <span className="muted" style={{ fontSize: "0.7rem" }}>
                {label}
              </span>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}
