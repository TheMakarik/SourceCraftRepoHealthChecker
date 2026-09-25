param(
    [string]$Path = $PSScriptRoot,
    [long]$MaxSizeBytes = 500MB
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

$relativePaths = @(git -C $repoRoot ls-files --cached --others --exclude-standard)

$totalBytes = [long]0
foreach ($relativePath in $relativePaths) {
    $fullPath = Join-Path $repoRoot $relativePath
    if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
        $totalBytes += (Get-Item -LiteralPath $fullPath -Force).Length
    }
}

$totalMegabytes = [math]::Round($totalBytes / 1MB, 2)
$limitMegabytes = [math]::Round($MaxSizeBytes / 1MB, 0)
Write-Host "Размер репозитория (без gitignored и .git): $totalMegabytes МБ ($totalBytes байт), файлов: $($relativePaths.Count)"

if ($totalBytes -gt $MaxSizeBytes) {
    Write-Error "Превышен лимит размера репозитория: $totalMegabytes МБ > $limitMegabytes МБ"
    exit 1
}

Write-Host "OK: не более $limitMegabytes МБ"
exit 0
