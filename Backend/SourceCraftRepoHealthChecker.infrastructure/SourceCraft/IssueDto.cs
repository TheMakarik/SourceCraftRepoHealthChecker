namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record IssueDto
{
    public string? Id { get; init; }
    public string? Slug { get; init; }
    public string? Title { get; init; }
    public IssueStatusDto? Status { get; init; }
    public UserEmbeddedDto? Author { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
