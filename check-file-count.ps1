param(
    [string]$Path = $PSScriptRoot,
    [int]$MaxFiles = 10000
)

if ($null -eq (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "git не найден в PATH"
    exit 1
}

$repoRoot = (git -C $Path rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    Write-Error "$Path не является git-репозиторием"
    exit 1
}

$files = @(git -C $repoRoot ls-files --cached --others --exclude-standard |
    Where-Object { Test-Path -LiteralPath (Join-Path $repoRoot $_) -PathType Leaf })

$count = $files.Count
Write-Host "Файлов в проекте (без .git и gitignored): $count"

if ($count -gt $MaxFiles) {
    Write-Error "Превышен лимит файлов: $count > $MaxFiles"
    exit 1
}

Write-Host "OK: не более $MaxFiles файлов"
exit 0
