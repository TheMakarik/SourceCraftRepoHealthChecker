namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record PublicRepositoryScore(
    string SourceCraftId,
    string FullName,
    int? Score,
    string? Grade,
    DateTimeOffset? AnalyzedAt,
    string MethodologyVersion,
    IReadOnlyCollection<PublicRepositoryScoreCategory> Categories);
