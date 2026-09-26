using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record RepositoryAnalysisMetric(
    MetricCode Code,
    double RawValue,
    double NormalizedScore,
    double Weight,
    DataStatus DataStatus);
