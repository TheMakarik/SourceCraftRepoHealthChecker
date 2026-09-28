namespace SourceCraftRepoHealthChecker.Application.Rating.Models;

public sealed record RepositoryLeaderboardItem(
    int Place,
    string SourceCraftId,
    string Name,
    string FullName,
    string Url,
    int? Score,
    int LikesCount,
    string Language,
    DateTimeOffset LastActivityAt,
    DateTimeOffset? AnalyzedAt);
