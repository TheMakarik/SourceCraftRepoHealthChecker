namespace SourceCraftRepoHealthChecker.Application.Rating.Models;

public sealed record RepositoryLeaderboardPage(
    IReadOnlyCollection<RepositoryLeaderboardItem> Items,
    int TotalCount,
    int Page,
    int PageSize);
