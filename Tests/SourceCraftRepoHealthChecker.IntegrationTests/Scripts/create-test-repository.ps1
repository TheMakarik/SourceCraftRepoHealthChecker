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
New-Item -ItemType Directory -Path (Join-Path $RepositoryPath '.github/workflows') -Force | Out-Null

Push-Location -LiteralPath $RepositoryPath
try {
    git init --initial-branch=main
    git config user.name 'Alice Developer'
    git config user.email 'alice@example.com'
    git config commit.gpgsign false

    $readme = @'
# Sample Project

## Getting Started

### Local run

```sh
dotnet run --project src/Sample.csproj
```

## Build and test

```sh
dotnet build
dotnet test
```
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath 'README.md') -Value $readme -Encoding utf8

    $license = @'
MIT License

Copyright (c) 2024 Sample Project

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
namespace Sample;

public static class Program
{
    public static void Main()
    {
        // TODO: refactor entry point
        // FIXME: handle missing configuration
    }
}
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath 'src/Program.cs') -Value $program -Encoding utf8

    $utils = @'
namespace Sample;

public static class Utils
{
    // FIXME: optimize this method
    public static int Add(int left, int right) => left + right;

    // TODO: add unit tests
}
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath 'src/Utils.cs') -Value $utils -Encoding utf8

    $ci = @'
name: ci

on:
  push:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      # TODO: add a lint step
      - run: echo build
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath '.github/workflows/ci.yml') -Value $ci -Encoding utf8

    git add --all

    $env:GIT_AUTHOR_DATE = '2024-01-10T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2024-01-10T10:00:00+00:00'
    git commit -m 'Initial project scaffold'

    $env:GIT_AUTHOR_DATE = '2024-02-15T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2024-02-15T10:00:00+00:00'
    git commit --allow-empty -m 'Add coding guidelines'

    git tag -a v1.0.0 -m 'First release'

    $env:GIT_AUTHOR_NAME = 'Bob Contributor'
    $env:GIT_AUTHOR_EMAIL = 'bob@example.com'
    $env:GIT_AUTHOR_DATE = '2024-03-20T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2024-03-20T10:00:00+00:00'
    git commit --allow-empty -m 'Fix utilities'
    Remove-Item Env:GIT_AUTHOR_NAME
    Remove-Item Env:GIT_AUTHOR_EMAIL

    $env:GIT_AUTHOR_NAME = 'dependabot[bot]'
    $env:GIT_AUTHOR_EMAIL = 'bot@example.com'
    $env:GIT_AUTHOR_DATE = '2024-04-25T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2024-04-25T10:00:00+00:00'
    git commit --allow-empty -m 'Bump dependencies'
    Remove-Item Env:GIT_AUTHOR_NAME
    Remove-Item Env:GIT_AUTHOR_EMAIL

    git tag -a v1.1.0 -m 'Second release'
    git branch feature/next
}
finally {
    Remove-Item Env:GIT_AUTHOR_DATE -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_COMMITTER_DATE -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_AUTHOR_NAME -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_AUTHOR_EMAIL -ErrorAction SilentlyContinue
    Pop-Location -ErrorAction SilentlyContinue
}
