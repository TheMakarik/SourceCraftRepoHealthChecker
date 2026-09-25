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

        var result = new HealthCheckResult(
            score,
            categories,
            recommendations,
            options.Value.MethodologyVersion,
            timeProvider.GetUtcNow());

        return Task.FromResult(result);
    }
}
