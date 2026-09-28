namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record RepositoryRatingDto
{
    public IReadOnlyCollection<ReactionCountDto>? ReactionCounts { get; init; }
}
