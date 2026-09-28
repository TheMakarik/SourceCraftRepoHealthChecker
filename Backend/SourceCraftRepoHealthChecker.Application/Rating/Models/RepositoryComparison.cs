namespace SourceCraftRepoHealthChecker.Application.Rating.Models;

public sealed record RepositoryComparison(IReadOnlyCollection<RepositoryComparisonItem> Items);
