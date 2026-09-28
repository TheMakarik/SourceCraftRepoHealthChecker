import { useState } from "react";
import { Tab, TabList } from "@fluentui/react-components";
import type { Analysis, ScoreCategory } from "../shared/api/types";
import { categoryLabels, categoryOrder } from "../shared/api/labels";
import { CategoryPanel } from "./CategoryPanel";

export function CategoryTabs({ analysis }: { analysis: Analysis }) {
  const [selected, setSelected] = useState<ScoreCategory>("Security");

  return (
    <div className="card stack">
      <TabList selectedValue={selected} onTabSelect={(_event, data) => setSelected(data.value as ScoreCategory)}>
        {categoryOrder.map((category) => (
          <Tab key={category} value={category}>
            {categoryLabels[category]}
          </Tab>
        ))}
      </TabList>
      <CategoryPanel analysis={analysis} category={selected} />
    </div>
  );
}
