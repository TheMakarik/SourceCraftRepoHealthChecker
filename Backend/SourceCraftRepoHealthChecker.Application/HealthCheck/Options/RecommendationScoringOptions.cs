namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class RecommendationScoringOptions
{
    public required int MinimumAcceptableScore { get; init; }
    public required int StrengthScore { get; init; }
    public required int HighPriorityScore { get; init; }
    public required int CriticalPriorityScore { get; init; }
}
