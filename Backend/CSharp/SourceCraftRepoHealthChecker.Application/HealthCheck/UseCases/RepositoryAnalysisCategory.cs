using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record RepositoryAnalysisCategory(
    ScoreCategory Category,
    int Score,
    DataStatus DataStatus);
