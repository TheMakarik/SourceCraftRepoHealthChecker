namespace SourceCraftRepoHealthChecker.Application.Rating.Models;

public sealed record RepositoryLeaderboardQuery(
    IReadOnlyCollection<string> Languages,
    RepositoryLeaderboardSort Sort,
    int Page,
    int PageSize)
{
    public IReadOnlyCollection<string> Languages { get; init; } = (Languages ?? [])
        .Where(language => !string.IsNullOrWhiteSpace(language))
        .Select(language => language.ToLowerInvariant())
        .Distinct()
        .ToArray();
}
