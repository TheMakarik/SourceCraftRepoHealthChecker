namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class CiCdScoringOptions
{
    public required int PipelineRunsForFullScore { get; init; }
    public required double MinimumSuccessRatio { get; init; }
    public required int MaxPipelineDurationMinutes { get; init; }
}
