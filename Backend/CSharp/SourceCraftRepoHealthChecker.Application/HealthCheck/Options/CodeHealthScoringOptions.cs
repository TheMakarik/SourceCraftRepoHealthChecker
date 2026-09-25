namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class CodeHealthScoringOptions
{
    public required double TodoPenalty { get; init; }
    public required double FixmePenalty { get; init; }
    public required int StaleCommentAgeDays { get; init; }
    public required double StaleCommentPenalty { get; init; }
    public required double MaxPenalty { get; init; }
}
