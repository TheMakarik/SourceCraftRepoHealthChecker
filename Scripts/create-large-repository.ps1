param(
    [string]$Path = (Join-Path ([System.IO.Path]::GetTempPath()) 'srhc-large-repository'),
    [int]$Files = 10000,
    [int]$Commits = 20,
    [int]$Authors = 5
)

# create-large-repository.ps1 — создаёт синтетический git-репозиторий, удовлетворяющий
# критерию «крупный репозиторий» из ТЗ (9.2/11.1): >= 10 000 файлов, или >= 20 000
# коммитов, или >= 500 МБ рабочей копии.
#
# Используется для сквозного прогона интеграционного теста:
#   Tests/SourceCraftRepoHealthChecker.IntegrationTests/LargeRepositoryTests
#   (Trait Category=LargeRepository, переменная SRHC_LARGE_REPO_PATH).
#
# Примеры:
#   ./Scripts/create-large-repository.ps1 -Path /tmp/srhc-large-repository
#   ./Scripts/create-large-repository.ps1 -Path /tmp/srhc-large-repository -Files 10000

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if ($Files -lt 1) { throw 'Параметр -Files должен быть положительным.' }
if ($Commits -lt 1) { throw 'Параметр -Commits должен быть положительным.' }
if ($Authors -lt 1) { throw 'Параметр -Authors должен быть положительным.' }

if (Test-Path -LiteralPath $Path) {
    Remove-Item -LiteralPath $Path -Recurse -Force
}

New-Item -ItemType Directory -Path $Path -Force | Out-Null

Push-Location -LiteralPath $Path
try {
    git init --initial-branch=main | Out-Null
    git config user.name 'Large Repository Author'
    git config user.email 'large-repository@example.com'
    git config commit.gpgsign false
    git config tag.gpgsign false

    $readme = @'
# Large Repository

## Getting Started

Run locally with the following commands.

## Build and test

```sh
dotnet build
dotnet test
```
'@
    [System.IO.File]::WriteAllText((Join-Path $Path 'README.md'), $readme)

    $license = @'
MIT License

Copyright (c) 2026 Large Repository

Permission is hereby granted, free of charge, to any person obtaining a copy.
'@
    [System.IO.File]::WriteAllText((Join-Path $Path 'LICENSE'), $license)

    $authorNames = @()
    $authorEmails = @()
    for ($authorIndex = 0; $authorIndex -lt $Authors; $authorIndex++) {
        $authorNames += "Author $authorIndex"
        $authorEmails += "author$authorIndex@example.com"
    }

    $filesPerCommit = [int][math]::Ceiling($Files / $Commits)
    $filesCreated = 0
    $baseDate = [datetimeoffset]::Parse('2024-01-01T10:00:00+00:00')
    $firstCommitTagged = $false

    for ($commitIndex = 0; $commitIndex -lt $Commits; $commitIndex++) {
        $authorIndex = $commitIndex % $Authors
        $env:GIT_AUTHOR_NAME = $authorNames[$authorIndex]
        $env:GIT_AUTHOR_EMAIL = $authorEmails[$authorIndex]
        $commitDate = $baseDate.AddMinutes($commitIndex).ToString('o')
        $env:GIT_AUTHOR_DATE = $commitDate
        $env:GIT_COMMITTER_DATE = $commitDate

        $filesThisCommit = [int][math]::Min($filesPerCommit, $Files - $filesCreated)

        if ($filesThisCommit -gt 0) {
            $batchDirectory = Join-Path $Path ('src/batch-{0:D5}' -f $commitIndex)
            New-Item -ItemType Directory -Path $batchDirectory -Force | Out-Null

            for ($fileIndex = 0; $fileIndex -lt $filesThisCommit; $fileIndex++) {
                $globalIndex = $filesCreated + $fileIndex
                $content = "namespace Large;`n`n// file $globalIndex`npublic static class File$globalIndex`n{`n    public static int Value => $globalIndex;`n}`n"

                if ($globalIndex % 10 -eq 0) {
                    $content += "// TODO: refactor file $globalIndex`n"
                }

                if ($globalIndex % 25 -eq 0) {
                    $content += "// FIXME: fix file $globalIndex`n"
                }

                [System.IO.File]::WriteAllText((Join-Path $batchDirectory ('File{0:D6}.cs' -f $globalIndex)), $content)
            }

            $filesCreated += $filesThisCommit
        }

        git add --all
        git commit --quiet --no-gpg-sign --allow-empty -m "Batch $commitIndex" | Out-Null

        if (-not $firstCommitTagged) {
            git tag -a v1.0.0 -m 'First release'
            $firstCommitTagged = $true
        }
    }

    git tag -a v2.0.0 -m 'Latest release'

    $trackedFiles = (git ls-files | Measure-Object).Count
    $commitCount = (git rev-list --count HEAD).Trim()
    $sizeMb = [math]::Round(((Get-ChildItem -LiteralPath $Path -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB), 1)

    Write-Host "Репозиторий создан: $Path"
    Write-Host "  Файлов (HEAD):  $trackedFiles"
    Write-Host "  Коммитов:       $commitCount"
    Write-Host "  Размер:         $sizeMb МБ"
    Write-Host "  Теги релизов:   $((git tag | Measure-Object).Count)"
    Write-Host ''
    Write-Host 'Критерий ТЗ выполнен: файлы >= 10000, или коммиты >= 20000, или размер >= 500 МБ.'
    Write-Host ''
    Write-Host 'Сквозной прогон:'
    Write-Host "  SRHC_LARGE_REPO_PATH=`"$Path`" dotnet test Tests/SourceCraftRepoHealthChecker.IntegrationTests --filter `"Category=LargeRepository`""
}
finally {
    Remove-Item Env:GIT_AUTHOR_DATE -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_COMMITTER_DATE -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_AUTHOR_NAME -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_AUTHOR_EMAIL -ErrorAction SilentlyContinue
    Pop-Location -ErrorAction SilentlyContinue
}
