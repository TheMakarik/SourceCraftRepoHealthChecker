using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

namespace SourceCraftRepoHealthChecker.Application.Rating.Models;

public sealed record RepositoryComparisonItem(
    string SourceCraftId,
    string Name,
    string FullName,
    string Url,
    string Language,
    int LikesCount,
    DateTimeOffset LastActivityAt,
    int? Score,
    DateTimeOffset? AnalyzedAt,
    IReadOnlyCollection<RepositoryAnalysisCategory> Categories,
    IReadOnlyCollection<RepositoryAnalysisMetric> Metrics,
    IReadOnlyCollection<RepositoryAnalysisCategory> Strengths,
    IReadOnlyCollection<RepositoryAnalysisCategory> Weaknesses);
