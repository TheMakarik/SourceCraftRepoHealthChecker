namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record PullRequestCommentDto
{
    public string? Id { get; init; }
    public UserEmbeddedDto? Author { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsPublished { get; init; }
}
