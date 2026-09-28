namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record ReactionCountDto
{
    public string? Type { get; init; }
    public string? Count { get; init; }
}
