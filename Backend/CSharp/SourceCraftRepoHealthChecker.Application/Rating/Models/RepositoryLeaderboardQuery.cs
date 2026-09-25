namespace SourceCraftRepoHealthChecker.Application.Rating.Models;

public sealed record RepositoryLeaderboardQuery(
    string? Language,
    RepositoryLeaderboardSort Sort,
    int Page,
    int PageSize);
