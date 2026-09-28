namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class HealthCheckOptions
{
    public required string MethodologyVersion { get; init; }
    public required ScoreScaleOptions ScoreScale { get; init; }
    public required CategoryWeightsOptions CategoryWeights { get; init; }
    public required SecurityScoringOptions Security { get; init; }
    public required CodeHealthScoringOptions CodeHealth { get; init; }
    public required ActivityScoringOptions Activity { get; init; }
    public required DocumentationScoringOptions Documentation { get; init; }
    public required CiCdScoringOptions CiCd { get; init; }
    public required IssuesScoringOptions Issues { get; init; }
    public required RecommendationScoringOptions Recommendations { get; init; }
}
