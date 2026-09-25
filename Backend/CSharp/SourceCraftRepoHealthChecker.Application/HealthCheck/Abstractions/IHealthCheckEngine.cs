using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;

public interface IHealthCheckEngine
{
    public Task<HealthCheckResult> CheckAsync(RepositoryFacts facts, CancellationToken cancellationToken);
}
