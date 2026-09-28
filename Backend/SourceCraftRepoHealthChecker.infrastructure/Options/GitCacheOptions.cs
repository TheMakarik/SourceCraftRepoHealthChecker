namespace SourceCraftRepoHealthChecker.infrastructure.Options;

public sealed class GitCacheOptions
{
    public int TtlMinutes { get; init; } = 10;

    public int MaxCachedRepositories { get; init; } = 8;
}
