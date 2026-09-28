import type { CategoryScore } from "../shared/api/types";
import { categoryLabels, categoryOrder, scoreTone, toneColor } from "../shared/api/labels";

export function CategoryCapsules({ categories }: { categories: CategoryScore[] }) {
  const byCategory = new Map(categories.map((item) => [item.category, item]));

  return (
    <div className="capsules">
      {categoryOrder.map((category) => {
        const item = byCategory.get(category);
        const score = item && item.dataStatus === "Available" ? item.score : null;
        const color = score === null ? "var(--srhc-border)" : toneColor(scoreTone(score));

        return (
          <div className="capsule" key={category}>
            <div className="capsule__track">
              <div className="capsule__fill" style={{ height: `${score ?? 0}%`, background: color }}>
                <span className="capsule__value" style={{ color }}>
                  {score === null ? "—" : score}
                </span>
              </div>
            </div>
            <span className="capsule__label">{categoryLabels[category]}</span>
          </div>
        );
      })}
    </div>
  );
}
