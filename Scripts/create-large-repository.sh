#!/usr/bin/env bash
# create-large-repository.sh — создаёт синтетический git-репозиторий, удовлетворяющий
# критерию «крупный репозиторий» из ТЗ (9.2/11.1): >= 10 000 файлов, или >= 20 000
# коммитов, или >= 500 МБ рабочей копии.
#
# Используется для сквозного прогона интеграционного теста:
#   Tests/SourceCraftRepoHealthChecker.IntegrationTests/LargeRepositoryTests
#   (Trait Category=LargeRepository, переменная SRHC_LARGE_REPO_PATH).
#
# Примеры:
#   ./Scripts/create-large-repository.sh --path /tmp/srhc-large-repository
#   ./Scripts/create-large-repository.sh --path /tmp/srhc-large-repository --files 10000
#   ./Scripts/create-large-repository.sh --path /tmp/srhc-large-repository --commits 20000
set -euo pipefail

TARGET="/tmp/srhc-large-repository"
FILES=10000
COMMITS=20
AUTHORS=5

usage() {
  cat <<'EOF'
Использование: create-large-repository.sh [--path <dir>] [--files <n>] [--commits <n>] [--authors <n>]

  --path     каталог, куда создать репозиторий (по умолчанию /tmp/srhc-large-repository)
  --files    число отслеживаемых файлов (по умолчанию 10000)
  --commits  число коммитов (по умолчанию 20)
  --authors  число авторов (по умолчанию 5)
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --path) TARGET="$2"; shift 2 ;;
    --files) FILES="$2"; shift 2 ;;
    --commits) COMMITS="$2"; shift 2 ;;
    --authors) AUTHORS="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Неизвестный аргумент: $1" >&2; usage >&2; exit 1 ;;
  esac
done

if ! [[ "$FILES" =~ ^[0-9]+$ ]] || [[ "$FILES" -lt 1 ]]; then
  echo "Параметр --files должен быть положительным целым." >&2
  exit 1
fi

if ! [[ "$COMMITS" =~ ^[0-9]+$ ]] || [[ "$COMMITS" -lt 1 ]]; then
  echo "Параметр --commits должен быть положительным целым." >&2
  exit 1
fi

if ! [[ "$AUTHORS" =~ ^[0-9]+$ ]] || [[ "$AUTHORS" -lt 1 ]]; then
  echo "Параметр --authors должен быть положительным целым." >&2
  exit 1
fi

if ! command -v git >/dev/null 2>&1; then
  echo "Не найден git." >&2
  exit 1
fi

rm -rf -- "$TARGET"
mkdir -p -- "$TARGET"

cd "$TARGET"

git init --initial-branch=main --quiet
git config user.name 'Large Repository Author'
git config user.email 'large-repository@example.com'
git config commit.gpgsign false
git config tag.gpgsign false

cat > README.md <<'EOF'
# Large Repository

## Getting Started

Run locally with the following commands.

## Build and test

```sh
dotnet build
dotnet test
```
EOF

cat > LICENSE <<'EOF'
MIT License

Copyright (c) 2026 Large Repository

Permission is hereby granted, free of charge, to any person obtaining a copy.
EOF

files_per_commit=$(( (FILES + COMMITS - 1) / COMMITS ))

# Индексы авторов.
for ((author_index = 0; author_index < AUTHORS; author_index++)); do
  author_names[$author_index]="Author $author_index"
  author_emails[$author_index]="author${author_index}@example.com"
done

files_created=0
first_commit_tagged=0
base_epoch="$(date -u -d '2024-01-01T10:00:00Z' +%s)"

for ((commit_index = 0; commit_index < COMMITS; commit_index++)); do
  author_index=$(( commit_index % AUTHORS ))
  export GIT_AUTHOR_NAME="${author_names[$author_index]}"
  export GIT_AUTHOR_EMAIL="${author_emails[$author_index]}"
  commit_date="$(date -u -d "@$(( base_epoch + commit_index * 60 ))" +%Y-%m-%dT%H:%M:%S+00:00)"
  export GIT_AUTHOR_DATE="$commit_date"
  export GIT_COMMITTER_DATE="$commit_date"

  files_this_commit=$(( files_per_commit ))
  if (( files_this_commit > FILES - files_created )); then
    files_this_commit=$(( FILES - files_created ))
  fi

  if (( files_this_commit > 0 )); then
    batch_dir="src/batch-$(printf '%05d' "$commit_index")"
    mkdir -p -- "$batch_dir"

    for ((file_index = 0; file_index < files_this_commit; file_index++)); do
      global_index=$(( files_created + file_index ))
      file_path="$batch_dir/File$(printf '%06d' "$global_index").cs"
      {
        printf 'namespace Large;\n\n// file %d\npublic static class File%d\n{\n    public static int Value => %d;\n}\n' \
          "$global_index" "$global_index" "$global_index"
        if (( global_index % 10 == 0 )); then
          printf '// TODO: refactor file %d\n' "$global_index"
        fi
        if (( global_index % 25 == 0 )); then
          printf '// FIXME: fix file %d\n' "$global_index"
        fi
      } > "$file_path"
    done

    files_created=$(( files_created + files_this_commit ))
  fi

  git add --all
  git commit --quiet --no-gpg-sign --allow-empty -m "Batch $commit_index"

  if (( first_commit_tagged == 0 )); then
    git tag -a v1.0.0 -m 'First release'
    first_commit_tagged=1
  fi
done

git tag -a v2.0.0 -m 'Latest release'

unset GIT_AUTHOR_NAME GIT_AUTHOR_EMAIL GIT_AUTHOR_DATE GIT_COMMITTER_DATE

tracked_files="$(git ls-files | wc -l | tr -d ' ')"
commit_count="$(git rev-list --count HEAD)"
size_human="$(du -sh . | cut -f1)"
size_mb="$(du -sm . | cut -f1)"

echo "Репозиторий создан: $TARGET"
echo "  Файлов (HEAD):  $tracked_files"
echo "  Коммитов:       $commit_count"
echo "  Размер:         $size_human (${size_mb} МБ)"
echo "  Теги релизов:   $(git tag | wc -l | tr -d ' ')"
echo
echo "Критерий ТЗ выполнен: файлы >= 10000, или коммиты >= 20000, или размер >= 500 МБ."
echo
echo "Сквозной прогон:"
echo "  SRHC_LARGE_REPO_PATH=\"$TARGET\" \\"
echo "    dotnet test Tests/SourceCraftRepoHealthChecker.IntegrationTests \\"
echo "      --filter \"Category=LargeRepository\""
