namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record IssueCommentDto
{
    public string? Id { get; init; }
    public UserEmbeddedDto? Author { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
