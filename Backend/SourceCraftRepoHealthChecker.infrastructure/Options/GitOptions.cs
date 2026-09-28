namespace SourceCraftRepoHealthChecker.infrastructure.Options;

public sealed class GitOptions
{
    public string? WorkDir
    {
        get => field;
        set => field = string.IsNullOrWhiteSpace(value) ? null : Environment.ExpandEnvironmentVariables(value);
    }

    public int CloneTimeoutSeconds { get; init; } = 900;

    public long MaxRepositoryMegabytes { get; init; } = 2048;

    public string? GitUsername { get; init; }

    public string? ServiceToken { get; init; }
}
