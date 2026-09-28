namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record RepositoryDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? DefaultBranch { get; init; }
    public OrganizationDto? Organization { get; init; }
    public bool IsEmpty { get; init; }
    public string? Visibility { get; init; }
    public string? WebUrl { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
    public RepositoryLanguageDto? Language { get; init; }
    public RepositoryRatingDto? Rating { get; init; }
}
