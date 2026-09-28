namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class ScoreScaleOptions
{
    public required int MinimumScore { get; init; }
    public required int MaximumScore { get; init; }
}
