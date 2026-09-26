$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet test SourceCraftRepoHealthChecker.slnx -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
