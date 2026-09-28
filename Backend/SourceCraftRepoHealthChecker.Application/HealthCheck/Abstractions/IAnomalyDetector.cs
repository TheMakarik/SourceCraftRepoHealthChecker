using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;

public interface IAnomalyDetector
{
    public IReadOnlyCollection<ActivityAnomaly> Detect(RepositoryFacts facts);
}
