param(
    [string]$Path = $PSScriptRoot,
    [int]$MaxFiles = 10000,
    [long]$MaxSizeBytes = 500MB
)

function Get-RepositoryRoot {
    param([string]$Path)

    if ($null -eq (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Error "git не найден в PATH"
        return $null
    }

    $repoRoot = git -C $Path rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
        Write-Error "$Path не является git-репозиторием"
        return $null
    }

    return $repoRoot
}

function Get-RepositoryFiles {
    param([string]$RepoRoot)

    $relativePaths = @(git -C $RepoRoot ls-files --cached --others --exclude-standard)

    $files = foreach ($relativePath in $relativePaths) {
        $fullPath = Join-Path $RepoRoot $relativePath
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            Get-Item -LiteralPath $fullPath -Force
        }
    }

    return @($files)
}

function Get-TotalFileSize {
    param([object[]]$Files)

    $totalBytes = [long]0
    foreach ($file in @($Files)) {
        $totalBytes += $file.Length
    }

    return $totalBytes
}

function Test-FileCountLimit {
    param([object[]]$Files, [int]$MaxFiles)

    $count = @($Files).Count
    Write-Host "Файлов в проекте (без .git и gitignored): $count (лимит $MaxFiles)"

    return $count -le $MaxFiles
}

function Test-RepositorySizeLimit {
    param([object[]]$Files, [long]$MaxSizeBytes)

    $totalBytes = Get-TotalFileSize -Files $Files
    $totalMegabytes = [math]::Round($totalBytes / 1MB, 2)
    $limitMegabytes = [math]::Round($MaxSizeBytes / 1MB, 0)
    Write-Host "Размер репозитория: $totalMegabytes МБ ($totalBytes байт) (лимит $limitMegabytes МБ)"

    return $totalBytes -le $MaxSizeBytes
}

$repoRoot = Get-RepositoryRoot -Path $Path
if ($null -eq $repoRoot) {
    exit 1
}

$files = Get-RepositoryFiles -RepoRoot $repoRoot

$results = @()
$results += Test-FileCountLimit -Files $files -MaxFiles $MaxFiles
$results += Test-RepositorySizeLimit -Files $files -MaxSizeBytes $MaxSizeBytes

if ($results -contains $false) {
    Write-Error "Проверки репозитория не пройдены"
    exit 1
}

Write-Host "OK: все проверки пройдены"
exit 0
