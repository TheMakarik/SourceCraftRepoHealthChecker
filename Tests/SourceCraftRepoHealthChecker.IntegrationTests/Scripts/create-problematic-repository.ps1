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

    $alpha = @'
namespace Legacy;

public static class Alpha
{
    // TODO: handle null configuration
    // TODO: add input validation
    // FIXME: remove duplication
    // TODO: cover with tests
    // FIXME: fix race condition
    public static int Run() => 0;
}
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath 'src/Alpha.cs') -Value $alpha -Encoding utf8

    $beta = @'
namespace Legacy;

public static class Beta
{
    // TODO: rename this method
    // FIXME: log errors
    public static int Stop() => 1;
}
'@
    Set-Content -LiteralPath (Join-Path $RepositoryPath 'src/Beta.cs') -Value $beta -Encoding utf8

    git add --all

    $env:GIT_AUTHOR_DATE = '2024-01-01T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2024-01-01T10:00:00+00:00'
    git commit -m 'Initial legacy import' | Out-Null

    $env:GIT_AUTHOR_DATE = '2024-01-02T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2024-01-02T10:00:00+00:00'
    git commit --allow-empty -m 'Patch release' | Out-Null

    $env:GIT_AUTHOR_DATE = '2024-01-03T10:00:00+00:00'
    $env:GIT_COMMITTER_DATE = '2024-01-03T10:00:00+00:00'
    git commit --allow-empty -m 'Final maintenance commit' | Out-Null
}
finally {
    Remove-Item Env:GIT_AUTHOR_DATE -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_COMMITTER_DATE -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_AUTHOR_NAME -ErrorAction SilentlyContinue
    Remove-Item Env:GIT_AUTHOR_EMAIL -ErrorAction SilentlyContinue
    Pop-Location -ErrorAction SilentlyContinue
}
