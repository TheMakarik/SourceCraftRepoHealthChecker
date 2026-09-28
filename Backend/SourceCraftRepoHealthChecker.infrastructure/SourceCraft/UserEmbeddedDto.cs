namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record UserEmbeddedDto
{
    public string? Id { get; init; }
    public string? Slug { get; init; }
}
