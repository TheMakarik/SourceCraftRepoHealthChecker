namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record IssueCommentPageDto
{
    public IReadOnlyCollection<IssueCommentDto>? IssueComments { get; init; }
    public string? NextPageToken { get; init; }
}
