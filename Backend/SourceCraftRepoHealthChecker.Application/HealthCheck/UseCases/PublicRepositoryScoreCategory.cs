using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record PublicRepositoryScoreCategory(
    ScoreCategory Category,
    int Score,
    DataStatus DataStatus);
