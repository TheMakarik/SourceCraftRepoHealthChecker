import { Badge } from "@fluentui/react-components";
import type { FolderReport } from "../shared/api/types";
import { scoreTone } from "../shared/api/labels";

interface FolderQuality {
  score: number;
  tone: "good" | "warn" | "bad";
}

export function folderQuality(folder: FolderReport): FolderQuality {
  let score = 40;
  if (folder.readme)
    score += 20;
  if (folder.license)
    score += 20;
  if (folder.tests)
    score += 20;
  score -= Math.min(folder.todoCount * 2, 20);
  score -= Math.min(folder.fixmeCount * 4, 20);
  const clamped = Math.max(0, Math.min(100, score));
  return { score: clamped, tone: scoreTone(clamped) };
}

const criterion = (present: boolean, label: string) => (
  <Badge appearance="tint" color={present ? "success" : "subtle"}>
    {label}
  </Badge>
);

export function FolderStats({ folder }: { folder: FolderReport }) {
  const quality = folderQuality(folder);

  return (
    <div className="folder-stats">
      <div className="folder-stats__head">
        <span className="folder-stats__path" title={folder.path}>{folder.path || "."}</span>
        <span className={`pill pill--${quality.tone}`}>{quality.score}</span>
      </div>
      <div className="metric-row">
        <span>Файлов в папке</span>
        <strong>{folder.files}</strong>
      </div>
      <div className="metric-row">
        <span>TODO / FIXME</span>
        <span className="row" style={{ gap: "0.35rem" }}>
          <Badge appearance="tint" color={folder.todoCount > 0 ? "warning" : "subtle"}>
            TODO: {folder.todoCount}
          </Badge>
          <Badge appearance="tint" color={folder.fixmeCount > 0 ? "danger" : "subtle"}>
            FIXME: {folder.fixmeCount}
          </Badge>
        </span>
      </div>
      <div className="metric-row">
        <span>Практики</span>
        <span className="row" style={{ gap: "0.35rem" }}>
          {criterion(folder.readme, "README")}
          {criterion(folder.license, "LICENSE")}
          {criterion(folder.tests, "TESTS")}
        </span>
      </div>
    </div>
  );
}
