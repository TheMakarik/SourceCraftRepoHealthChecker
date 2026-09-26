$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location (Join-Path $root 'Backend/Go')

$unformatted = gofmt -l .
if ($unformatted) {
    Write-Error "gofmt found unformatted files:`n$unformatted"
    exit 1
}

go build ./...
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

go vet ./...
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
