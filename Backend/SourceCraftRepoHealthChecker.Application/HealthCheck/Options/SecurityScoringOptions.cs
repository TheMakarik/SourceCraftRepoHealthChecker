namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class SecurityScoringOptions
{
    public required double CriticalPenalty { get; init; }
    public required double HighPenalty { get; init; }
    public required double MediumPenalty { get; init; }
    public required double LowPenalty { get; init; }
    public required double FixedFindingCredit { get; init; }
}
