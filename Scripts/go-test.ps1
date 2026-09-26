$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location (Join-Path $root 'Backend/Go')

go test ./...
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
