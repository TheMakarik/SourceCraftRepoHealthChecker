using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Services;

public sealed class MetricNormalizer(IOptions<HealthCheckOptions> options) : IMetricNormalizer
{
    public double Normalize(double value, double worstValue, double bestValue)
    {
        var scale = options.Value.ScoreScale;

        if (worstValue == bestValue)
            return value == bestValue ? scale.MaximumScore : scale.MinimumScore;

        var ratio = (value - worstValue) / (bestValue - worstValue);
        var score = ratio * (scale.MaximumScore - scale.MinimumScore) + scale.MinimumScore;

        return Math.Clamp(score, scale.MinimumScore, scale.MaximumScore);
    }
}
