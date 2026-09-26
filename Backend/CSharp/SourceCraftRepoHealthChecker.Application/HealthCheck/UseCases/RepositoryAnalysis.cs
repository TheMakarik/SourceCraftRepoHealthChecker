namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record RepositoryAnalysis(
    string SourceCraftId,
    string Name,
    string FullName,
    string Url,
    string Language,
    bool IsPrivate,
    Guid? OwnerUserId,
    int LikesCount,
    int Score,
    DateTimeOffset? AnalyzedAt,
    IReadOnlyCollection<RepositoryAnalysisCategory> Categories,
    IReadOnlyCollection<RepositoryAnalysisMetric> Metrics,
    IReadOnlyCollection<RepositoryAnalysisCategory> Strengths,
    IReadOnlyCollection<RepositoryAnalysisCategory> Weaknesses,
    IReadOnlyCollection<RepositoryAnalysisRecommendation> Recommendations);
