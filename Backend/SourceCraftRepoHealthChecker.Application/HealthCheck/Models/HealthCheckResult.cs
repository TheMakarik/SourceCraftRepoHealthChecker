using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public sealed record HealthCheckResult(
    int? Score,
    DataStatus DataStatus,
    IReadOnlyCollection<CategoryScoreResult> Categories,
    IReadOnlyCollection<RecommendationDraft> Recommendations,
    IReadOnlyCollection<CategoryHighlight> Strengths,
    IReadOnlyCollection<CategoryHighlight> Weaknesses,
    string MethodologyVersion,
    DateTimeOffset CalculatedAt);
