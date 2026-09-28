using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record AnalyzeRepositoryResult(
    HealthCheckResult HealthCheck,
    Guid AnalysisRunId);
