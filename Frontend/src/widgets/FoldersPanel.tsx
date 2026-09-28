import { Badge, Spinner } from "@fluentui/react-components";
import type { FolderReport } from "../shared/api/types";
import { scoreTone } from "../shared/api/labels";
import { useFolders } from "../shared/api/hooks";

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

export function FoldersPanel({ repositoryId }: { repositoryId: string | undefined }) {
  const folders = useFolders(repositoryId);
  const items = [...(folders.data ?? [])].sort((left, right) => left.path.localeCompare(right.path));

  if (folders.isPending)
    return (
      <div className="row">
        <Spinner size="tiny" />
        <span className="muted">Загружаем папки…</span>
      </div>
    );

  if (folders.isError)
    return <span className="muted">Не удалось загрузить список папок.</span>;

  if (items.length === 0)
    return <span className="muted">Папки не найдены.</span>;

  return (
    <div className="folder-list">
      <div className="folder-list__head">
        <span>Папка</span>
        <span>Файлы</span>
        <span>Маркеры</span>
        <span>Практики</span>
        <span className="folder-list__score">Индикатор</span>
      </div>
      {items.map((folder) => {
        const quality = folderQuality(folder);
        return (
          <div className="folder-row" key={folder.path}>
            <span className="folder-row__path" title={folder.path}>{folder.path || "."}</span>
            <span>{folder.files}</span>
            <span className="row" style={{ gap: "0.35rem" }}>
              <Badge appearance="tint" color={folder.todoCount > 0 ? "warning" : "subtle"}>
                TODO: {folder.todoCount}
              </Badge>
              <Badge appearance="tint" color={folder.fixmeCount > 0 ? "danger" : "subtle"}>
                FIXME: {folder.fixmeCount}
              </Badge>
            </span>
            <span className="row" style={{ gap: "0.35rem" }}>
              {criterion(folder.readme, "README")}
              {criterion(folder.license, "LICENSE")}
              {criterion(folder.tests, "TESTS")}
            </span>
            <span className="folder-list__score">
              <span className={`pill pill--${quality.tone}`}>{quality.score}</span>
            </span>
          </div>
        );
      })}
    </div>
  );
}
