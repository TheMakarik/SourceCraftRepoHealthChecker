using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;

public interface IHealthScoreCalculator
{
    public int Calculate(IReadOnlyCollection<CategoryScoreResult> categories);
}
