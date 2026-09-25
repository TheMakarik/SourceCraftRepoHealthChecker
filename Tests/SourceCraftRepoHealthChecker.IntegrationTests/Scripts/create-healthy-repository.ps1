param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$RepositoryPath
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if (Test-Path -LiteralPath $RepositoryPath) {
    Remove-Item -LiteralPath $RepositoryPath -Recurse -Force
}

New-Item -ItemType Directory -Path $RepositoryPath -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $RepositoryPath 'src') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $RepositoryPath '.github') -Force | Out-Null

Push-Location -LiteralPath $RepositoryPath
try {
    git init --initial-branch=main | Out-Null
    git config user.name 'Alice Developer'
    git config user.email 'alice@example.com'
    git config commit.gpgsign false
    git config tag.gpgsign false

    $readme = @'
# Healthy Project

## Getting Started

Run locally with the following command.

## Build and test

dotnet build
dotnet test
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath 'README.md') -Value $readme -Encoding utf8

    $license = @'
MIT License

Copyright (c) 2026 Healthy Project

Permission is hereby granted, free of charge, to any person obtaining a copy.
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath 'LICENSE') -Value $license -Encoding utf8

    $contributing = @'
# Contributing

Thanks for contributing. Please open an issue or a merge request.
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath 'CONTRIBUTING.md') -Value $contributing -Encoding utf8

    $codeowners = @'
* @alice
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath '.github/CODEOWNERS') -Value $codeowners -Encoding utf8

    $program = @'
namespace Healthy;

public static class Program
{
    public static int Add(int left, int right) => left + right;
}
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath 'src/Program.cs') -Value $program -Encoding utf8

    git add --all

    $env:GIT_AUTHOR_DATE = '2026-01-05T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2026-01-05T10:00:00+00:00'
    git commit -m 'Initial project scaffold' | Out-Null

    $env:GIT_AUTHOR_DATE = '2026-01-08T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2026-01-08T10:00:00+00:00'
    git commit --allow-empty -m 'Refine utilities' | Out-Null

    $env:GIT_AUTHOR_NAME = 'Bob Contributor'
    $env:GIT_AUTHOR_EMAIL = 'bob@example.com'
    $env:GIT_AUTHOR_DATE = '2026-01-12T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2026-01-12T10:00:00+00:00'
    git commit --allow-empty -m 'Add integration helper' | Out-Null
    Remove-Item Env:GIT_AUTHOR_NAME
    Remove-Item Env:GIT_AUTHOR_EMAIL

    git tag -a v1.0.0 -m 'First release'

    $env:GIT_AUTHOR_NAME = 'Carol Maintainer'
    $env:GIT_AUTHOR_EMAIL = 'carol@example.com'
    $env:GIT_AUTHOR_DATE = '2026-01-18T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2026-01-18T10:00:00+00:00'
    git commit --allow-empty -m 'Improve documentation' | Out-Null
    Remove-Item Env:GIT_AUTHOR_NAME
    Remove-Item Env:GIT_AUTHOR_EMAIL

    $env:GIT_AUTHOR_DATE = '2026-01-24T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2026-01-24T10:00:00+00:00'
    git commit --allow-empty -m 'Final polish' | Out-Null

    git tag -a v1.1.0 -m 'Second release'
}
finally {
    Remove-Item Env:GIT_AUTHOR_DATE -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_COMMITTER_DATE -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_AUTHOR_NAME -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_AUTHOR_EMAIL -ErrorAction SilentlyContinue
    Pop-Location -ErrorAction SilentlyContinue
}
