namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record PullRequestCommentPageDto
{
    public IReadOnlyCollection<PullRequestCommentDto>? PullRequestComments { get; init; }
    public string? NextPageToken { get; init; }
}
