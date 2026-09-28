namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record RepositoryPageDto
{
    public IReadOnlyCollection<RepositoryDto>? Repositories { get; init; }
    public string? NextPageToken { get; init; }
}
