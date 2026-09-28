namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record PullRequestDto
{
    public string? Id { get; init; }
    public string? Slug { get; init; }
    public string? Title { get; init; }
    public UserEmbeddedDto? Author { get; init; }
    public string? Status { get; init; }
    public string? SourceBranch { get; init; }
    public string? TargetBranch { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
