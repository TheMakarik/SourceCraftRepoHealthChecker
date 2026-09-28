namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record CloneUrlDto
{
    public string? Https { get; init; }
    public string? Ssh { get; init; }
}
