namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record IssuePageDto
{
    public IReadOnlyCollection<IssueDto>? Issues { get; init; }
    public string? NextPageToken { get; init; }
}
