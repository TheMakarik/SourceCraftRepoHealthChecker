using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;

public interface IRecommendationGenerator
{
    public IReadOnlyCollection<RecommendationDraft> Generate(IReadOnlyCollection<CategoryScoreResult> categories);
}
