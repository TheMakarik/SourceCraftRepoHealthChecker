using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;

public interface ICategoryScoreCalculator
{
    public CategoryScoreResult Calculate(ScoreCategory category, RepositoryFacts facts);
}
