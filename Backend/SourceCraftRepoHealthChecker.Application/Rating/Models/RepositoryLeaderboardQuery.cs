namespace SourceCraftRepoHealthChecker.Application.Rating.Models;

public sealed record RepositoryLeaderboardQuery(
    IReadOnlyCollection<string> Languages,
    RepositoryLeaderboardSort Sort,
    int Page,
    int PageSize,
    bool? HasCi = null,
    int? MinScore = null,
    int? MaxScore = null)
{
    public IReadOnlyCollection<string> Languages { get; init; } = (Languages ?? [])
        .Where(language => !string.IsNullOrWhiteSpace(language))
        .Select(language => language.ToLowerInvariant())
        .Distinct()
        .ToArray();

    public bool IsScoreRangeOrdered => MinScore is null || MaxScore is null || MinScore <= MaxScore;
}
