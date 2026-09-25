namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public sealed record HealthCheckResult(
    int Score,
    IReadOnlyCollection<CategoryScoreResult> Categories,
    IReadOnlyCollection<RecommendationDraft> Recommendations,
    string MethodologyVersion,
    DateTimeOffset CalculatedAt);
