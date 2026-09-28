namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record PullRequestPageDto
{
    public IReadOnlyCollection<PullRequestDto>? PullRequests { get; init; }
    public string? NextPageToken { get; init; }
}
