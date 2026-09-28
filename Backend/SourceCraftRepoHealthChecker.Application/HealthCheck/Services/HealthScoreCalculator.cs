using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Services;

public sealed class HealthScoreCalculator(IOptions<HealthCheckOptions> options) : IHealthScoreCalculator
{
    public int Calculate(IReadOnlyCollection<CategoryScoreResult> categories)
    {
        var scale = options.Value.ScoreScale;
        var available = categories.Where(x => x.DataStatus == DataStatus.Available).ToArray();

        if (available.Length == 0)
            return scale.MinimumScore;

        var totalWeight = available.Sum(x => x.Weight);
        if (totalWeight <= 0)
            return scale.MinimumScore;

        var weighted = available.Sum(x => x.Score * x.Weight);
        var score = (int)Math.Round(weighted / totalWeight, MidpointRounding.AwayFromZero);

        return Math.Clamp(score, scale.MinimumScore, scale.MaximumScore);
    }
}
