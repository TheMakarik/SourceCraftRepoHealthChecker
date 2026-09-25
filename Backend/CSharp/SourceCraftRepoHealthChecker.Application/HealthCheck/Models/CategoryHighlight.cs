using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public sealed record CategoryHighlight(
    ScoreCategory Category,
    int Score,
    DataStatus DataStatus);
