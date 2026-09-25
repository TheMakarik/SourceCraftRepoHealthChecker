using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Services;

public sealed class HealthCheckEngine(
    IOptions<HealthCheckOptions> options,
    ICategoryScoreCalculator categoryScoreCalculator,
    IHealthScoreCalculator healthScoreCalculator,
    IRecommendationGenerator recommendationGenerator,
    TimeProvider timeProvider) : IHealthCheckEngine
{
    public Task<HealthCheckResult> CheckAsync(RepositoryFacts facts, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var categories = Enum.GetValues<ScoreCategory>()
            .Select(category => categoryScoreCalculator.Calculate(category, facts))
            .ToArray();

        var score = healthScoreCalculator.Calculate(categories);
        var recommendations = recommendationGenerator.Generate(categories);
        var highlights = options.Value.Recommendations;
        var available = categories.Where(category => category.DataStatus == DataStatus.Available).ToArray();

        var strengths = available
            .Where(category => category.Score >= highlights.StrengthScore)
            .Select(category => new CategoryHighlight(category.Category, category.Score, category.DataStatus))
            .ToArray();
        var weaknesses = available
            .Where(category => category.Score < highlights.MinimumAcceptableScore)
            .Select(category => new CategoryHighlight(category.Category, category.Score, category.DataStatus))
            .ToArray();

        var result = new HealthCheckResult(
            score,
            categories,
            recommendations,
            strengths,
            weaknesses,
            options.Value.MethodologyVersion,
            timeProvider.GetUtcNow());

        return Task.FromResult(result);
    }
}
