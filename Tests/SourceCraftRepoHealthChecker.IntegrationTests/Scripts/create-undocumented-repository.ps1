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

Push-Location -LiteralPath $RepositoryPath
try {
    git init --initial-branch=main | Out-Null
    git config user.name 'Alice Developer'
    git config user.email 'alice@example.com'
    git config commit.gpgsign false
    git config tag.gpgsign false

    $service = @'
namespace Service;

public static class WidgetService
{
    public static int Count() => 42;
}
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath 'src/WidgetService.cs') -Value $service -Encoding utf8

    git add --all

    $env:GIT_AUTHOR_DATE = '2026-01-10T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2026-01-10T10:00:00+00:00'
    git commit -m 'Initial service' | Out-Null

    $env:GIT_AUTHOR_NAME = 'Bob Contributor'
    $env:GIT_AUTHOR_EMAIL = 'bob@example.com'
    $env:GIT_AUTHOR_DATE = '2026-01-14T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2026-01-14T10:00:00+00:00'
    git commit --allow-empty -m 'Extend service' | Out-Null
    Remove-Item Env:GIT_AUTHOR_NAME
    Remove-Item Env:GIT_AUTHOR_EMAIL

    $env:GIT_AUTHOR_NAME = 'Carol Maintainer'
    $env:GIT_AUTHOR_EMAIL = 'carol@example.com'
    $env:GIT_AUTHOR_DATE = '2026-01-20T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2026-01-20T10:00:00+00:00'
    git commit --allow-empty -m 'Refactor service' | Out-Null
    Remove-Item Env:GIT_AUTHOR_NAME
    Remove-Item Env:GIT_AUTHOR_EMAIL

    git tag -a v1.0.0 -m 'First release'

    $env:GIT_AUTHOR_NAME = 'Dave Reviewer'
    $env:GIT_AUTHOR_EMAIL = 'dave@example.com'
    $env:GIT_AUTHOR_DATE = '2026-01-25T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2026-01-25T10:00:00+00:00'
    git commit --allow-empty -m 'Review fixes' | Out-Null
    Remove-Item Env:GIT_AUTHOR_NAME
    Remove-Item Env:GIT_AUTHOR_EMAIL

    $env:GIT_AUTHOR_DATE = '2026-01-28T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2026-01-28T10:00:00+00:00'
    git commit --allow-empty -m 'Ship next iteration' | Out-Null

    git tag -a v2.0.0 -m 'Second release'
}
finally {
    Remove-Item Env:GIT_AUTHOR_DATE -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_COMMITTER_DATE -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_AUTHOR_NAME -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_AUTHOR_EMAIL -ErrorAction SilentlyContinue
    Pop-Location -ErrorAction SilentlyContinue
}
