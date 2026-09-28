import { useState } from "react";
import { Spinner, Tree, TreeItem, TreeItemLayout } from "@fluentui/react-components";
import { Document24Regular, Folder24Regular } from "@fluentui/react-icons";
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";
import { oneDark } from "react-syntax-highlighter/dist/esm/styles/prism";
import type { RepositoryTreeEntry } from "../shared/api/types";
import { useRepositoryFile, useTree } from "../shared/api/hooks";
import { appConfig } from "../shared/config";

interface TreeProps {
  repositoryId: string | undefined;
  entry: RepositoryTreeEntry;
  selectedPath: string | null;
  onSelect: (path: string) => void;
}

function FileNode({ repositoryId, entry, selectedPath, onSelect }: TreeProps) {
  const [open, setOpen] = useState(false);
  const isDirectory = entry.type === "directory";
  const tree = useTree(repositoryId, entry.path, isDirectory && open);

  if (!isDirectory)
    return (
      <TreeItem itemType="leaf" value={entry.path}>
        <TreeItemLayout
          iconBefore={<Document24Regular />}
          className={selectedPath === entry.path ? "file-node--selected" : undefined}
          onClick={() => onSelect(entry.path)}
        >
          {entry.name}
        </TreeItemLayout>
      </TreeItem>
    );

  const children = tree.data?.entries ?? [];

  return (
    <TreeItem
      itemType="branch"
      value={entry.path}
      open={open}
      onOpenChange={(_event, data) => setOpen(data.open)}
    >
      <TreeItemLayout iconBefore={<Folder24Regular />}>{entry.name}</TreeItemLayout>
      {open
        ? tree.isPending
          ? (
            <TreeItem itemType="leaf" value={`${entry.path}::loading`}>
              <TreeItemLayout>Загрузка…</TreeItemLayout>
            </TreeItem>
          )
          : tree.isError
            ? (
              <TreeItem itemType="leaf" value={`${entry.path}::error`}>
                <TreeItemLayout>Не удалось загрузить</TreeItemLayout>
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

function FileViewer({ repositoryId, path }: { repositoryId: string | undefined; path: string }) {
  const file = useRepositoryFile(repositoryId, path);

  if (file.isPending)
    return (
      <div className="row">
        <Spinner size="tiny" />
        <span className="muted">Загружаем файл…</span>
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

export function FileBrowser({ repositoryId }: { repositoryId: string | undefined }) {
  const root = useTree(repositoryId, "", true);
  const [selectedPath, setSelectedPath] = useState<string | null>(null);
  const entries = root.data?.entries ?? [];

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
          <Tree aria-label="Файлы репозитория">
            {entries.map((entry) => (
              <FileNode
                key={entry.path}
                repositoryId={repositoryId}
                entry={entry}
                selectedPath={selectedPath}
                onSelect={setSelectedPath}
              />
            ))}
          </Tree>
        )}
        {root.data?.truncated ? <span className="tone-warn" style={{ fontSize: "0.8rem" }}>Список усечён.</span> : null}
      </div>
      <div className="file-browser__viewer">
        {selectedPath
          ? <FileViewer repositoryId={repositoryId} path={selectedPath} />
          : <span className="muted">Выберите файл, чтобы посмотреть содержимое.</span>}
      </div>
    </div>
  );
}
