namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record ReleaseDto
{
    public string? Id { get; init; }
    public string? Tag { get; init; }
    public string? Title { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReleasedAt { get; init; }
}
