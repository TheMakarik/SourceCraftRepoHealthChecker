namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal sealed class GitCacheEntry
{
    public required string Path { get; init; }

    public DateTimeOffset LastAccessUtc { get; set; }
}
