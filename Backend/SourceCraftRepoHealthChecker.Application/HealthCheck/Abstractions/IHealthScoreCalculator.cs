using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;

public interface IHealthScoreCalculator
{
    public HealthScoreResult Calculate(IReadOnlyCollection<CategoryScoreResult> categories);
}
