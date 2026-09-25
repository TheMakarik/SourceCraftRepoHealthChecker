namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public sealed record HealthCheckResult(
    int Score,
    IReadOnlyCollection<CategoryScoreResult> Categories,
    IReadOnlyCollection<RecommendationDraft> Recommendations,
    IReadOnlyCollection<CategoryHighlight> Strengths,
    IReadOnlyCollection<CategoryHighlight> Weaknesses,
    string MethodologyVersion,
    DateTimeOffset CalculatedAt);
