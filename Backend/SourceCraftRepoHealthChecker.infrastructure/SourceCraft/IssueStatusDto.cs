namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record IssueStatusDto
{
    public string? Id { get; init; }
    public string? Slug { get; init; }
    public string? Name { get; init; }
    public string? StatusType { get; init; }
}
