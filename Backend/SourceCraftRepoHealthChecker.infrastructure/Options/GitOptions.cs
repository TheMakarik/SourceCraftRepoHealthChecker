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

    public int ShallowCloneDepth { get; init; }

    public int CloneMaxAttempts { get; init; } = 3;

    public int CloneRetryDelayMilliseconds { get; init; } = 1000;

    public int CloneRetryMaxDelayMilliseconds { get; init; } = 30000;

    public string? GitUsername { get; init; }

    public string? ServiceToken { get; init; }
}
