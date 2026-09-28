import { useMemo, useState } from "react";
import { Spinner, Tree, TreeItem, TreeItemLayout } from "@fluentui/react-components";
import { Document24Regular, Folder24Regular } from "@fluentui/react-icons";
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";
import { oneDark } from "react-syntax-highlighter/dist/esm/styles/prism";
import type { FolderReport, RepositoryTreeEntry, RepositoryTreeSelection } from "../shared/api/types";
import { useFolders, useRepositoryFile, useTree } from "../shared/api/hooks";
import { appConfig } from "../shared/config";
import { FolderStats } from "./FoldersPanel";

interface TreeProps {
  repositoryId: string | undefined;
  entry: RepositoryTreeEntry;
  selectedPath: string | null;
  onSelect: (selection: RepositoryTreeSelection) => void;
}

const sortEntries = (entries: RepositoryTreeEntry[]) =>
  [...entries].sort((left, right) => {
    if (left.type !== right.type)
      return left.type === "directory" ? -1 : 1;
    return left.name.localeCompare(right.name);
  });

function FileNode({ repositoryId, entry, selectedPath, onSelect }: TreeProps) {
  const [open, setOpen] = useState(false);
  const isDirectory = entry.type === "directory";
  const tree = useTree(repositoryId, entry.path, isDirectory && open);
  const selected = selectedPath === entry.path;

  if (!isDirectory)
    return (
      <TreeItem itemType="leaf" value={entry.path}>
        <TreeItemLayout
          iconBefore={<Document24Regular />}
          className={selected ? "file-node--selected" : undefined}
          onClick={() => onSelect({ path: entry.path, type: entry.type })}
        >
          {entry.name}
        </TreeItemLayout>
      </TreeItem>
    );

  const children = sortEntries(tree.data?.entries ?? []);

  return (
    <TreeItem
      itemType="branch"
      value={entry.path}
      open={open}
      onOpenChange={(_event, data) => setOpen(data.open)}
    >
      <TreeItemLayout
        iconBefore={<Folder24Regular />}
        className={selected ? "file-node--selected" : undefined}
        onClick={() => onSelect({ path: entry.path, type: entry.type })}
      >
        {entry.name}
      </TreeItemLayout>
      {open
        ? tree.isPending
          ? (
            <TreeItem itemType="leaf" value={`${entry.path}::loading`}>
              <TreeItemLayout>
                <Spinner size="tiny" />
                <span className="muted">Загрузка…</span>
              </TreeItemLayout>
            </TreeItem>
          )
          : tree.isError
            ? (
              <TreeItem itemType="leaf" value={`${entry.path}::error`}>
                <TreeItemLayout>
                  <span className="muted">Не удалось загрузить</span>
                </TreeItemLayout>
              </TreeItem>
            )
            : children.length === 0
              ? (
                <TreeItem itemType="leaf" value={`${entry.path}::empty`}>
                  <TreeItemLayout>
                    <span className="muted">Пусто</span>
                  </TreeItemLayout>
                </TreeItem>
              )
              : children.map((child) => (
                <FileNode
                  key={child.path}
                  repositoryId={repositoryId}
                  entry={child}
                  selectedPath={selectedPath}
                  onSelect={onSelect}
                />
              ))
        : null}
    </TreeItem>
  );
}

function FileSkeleton() {
  return (
    <div className="file-skeleton" aria-hidden="true">
      {Array.from({ length: 8 }).map((_, index) => (
        <span
          key={index}
          className="file-skeleton__line"
          style={{ width: `${60 + ((index * 13) % 35)}%` }}
        />
      ))}
    </div>
  );
}

function FileViewer({ repositoryId, path }: { repositoryId: string | undefined; path: string }) {
  const file = useRepositoryFile(repositoryId, path);

  if (file.isPending)
    return (
      <div className="stack" style={{ gap: "0.5rem" }}>
        <div className="row">
          <Spinner size="tiny" />
          <span className="muted">Загружаем файл…</span>
        </div>
        <FileSkeleton />
      </div>
    );

  if (file.isError)
    return <span className="tone-bad">Не удалось загрузить файл.</span>;

  if (!file.data)
    return null;

  const data = file.data;
  if (data.binary)
    return <span className="muted">Бинарный файл — предпросмотр недоступен.</span>;

  const truncatedByClient = data.content.length > appConfig.maxRenderedFileBytes;
  const content = truncatedByClient ? data.content.slice(0, appConfig.maxRenderedFileBytes) : data.content;

  return (
    <div className="stack" style={{ gap: "0.5rem" }}>
      <div className="row" style={{ justifyContent: "space-between" }}>
        <span className="muted" style={{ fontSize: "0.8rem" }}>{data.path}</span>
        {data.truncated || truncatedByClient ? <span className="tone-warn">Файл усечён</span> : null}
      </div>
      <SyntaxHighlighter
        language={data.language || "text"}
        style={oneDark}
        showLineNumbers
        wrapLongLines
        customStyle={{ margin: 0, borderRadius: "0.5rem", fontSize: "0.8rem", maxHeight: "32rem" }}
      >
        {content}
      </SyntaxHighlighter>
    </div>
  );
}

interface FolderViewProps {
  selection: RepositoryTreeSelection;
  folder: FolderReport | null;
  isPending: boolean;
  isError: boolean;
}

function FolderView({ selection, folder, isPending, isError }: FolderViewProps) {
  if (isPending)
    return (
      <div className="row">
        <Spinner size="tiny" />
        <span className="muted">Загружаем статистику папки…</span>
      </div>
    );

  if (isError)
    return <span className="muted">Не удалось загрузить статистику папок.</span>;

  if (!folder)
    return (
      <div className="stack" style={{ gap: "0.35rem" }}>
        <span className="folder-stats__path" title={selection.path}>{selection.path || "."}</span>
        <span className="muted">Нет данных по этой папке — в ней нет файлов или она не проанализирована.</span>
      </div>
    );

  return <FolderStats folder={folder} />;
}

export function FileBrowser({ repositoryId }: { repositoryId: string | undefined }) {
  const root = useTree(repositoryId, "", true);
  const folders = useFolders(repositoryId);
  const [selection, setSelection] = useState<RepositoryTreeSelection | null>(null);
  const entries = sortEntries(root.data?.entries ?? []);

  const folderByPath = useMemo(() => {
    const map = new Map<string, FolderReport>();
    for (const folder of folders.data ?? [])
      map.set(folder.path, folder);
    return map;
  }, [folders.data]);

  const selectedFolder = selection?.type === "directory"
    ? folderByPath.get(selection.path) ?? null
    : null;

  return (
    <div className="file-browser">
      <div className="file-browser__tree">
        {root.isPending ? (
          <div className="row">
            <Spinner size="tiny" />
            <span className="muted">Загружаем дерево…</span>
          </div>
        ) : root.isError ? (
          <span className="muted">Не удалось загрузить дерево файлов.</span>
        ) : entries.length === 0 ? (
          <span className="muted">Файлы не найдены.</span>
        ) : (
          <Tree aria-label="Файлы и папки репозитория">
            {entries.map((entry) => (
              <FileNode
                key={entry.path}
                repositoryId={repositoryId}
                entry={entry}
                selectedPath={selection?.path ?? null}
                onSelect={setSelection}
              />
            ))}
          </Tree>
        )}
        {root.data?.truncated ? <span className="tone-warn" style={{ fontSize: "0.8rem" }}>Список усечён.</span> : null}
      </div>
      <div className="file-browser__viewer">
        {!selection ? (
          <span className="muted">Выберите файл или папку, чтобы посмотреть содержимое и статистику.</span>
        ) : selection.type === "directory" ? (
          <FolderView
            selection={selection}
            folder={selectedFolder}
            isPending={folders.isPending}
            isError={folders.isError}
          />
        ) : (
          <FileViewer repositoryId={repositoryId} path={selection.path} />
        )}
      </div>
    </div>
  );
}
