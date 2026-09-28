using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public sealed record CategoryScoreResult(
    ScoreCategory Category,
    int Score,
    double Weight,
    DataStatus DataStatus,
    IReadOnlyCollection<MetricScore> Metrics);
