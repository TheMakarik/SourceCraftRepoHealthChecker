using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public sealed record MetricScore(
    MetricCode Code,
    double RawValue,
    double NormalizedScore,
    double Weight,
    DataStatus DataStatus);
