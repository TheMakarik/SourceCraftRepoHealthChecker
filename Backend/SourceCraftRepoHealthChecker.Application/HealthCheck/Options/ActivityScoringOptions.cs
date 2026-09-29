namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class ActivityScoringOptions
{
    public required int ActiveWithinDays { get; init; }
    public required int StaleAfterDays { get; init; }
    public required int CommitFrequencyForFullScore { get; init; }
    public required int ContributorsForFullScore { get; init; }
    public required int ReleasesForFullScore { get; init; }
    public required int MergeRequestsForFullScore { get; init; }
    public int TrendWindowDays { get; init; } = 30;
    public double TrendWeight { get; init; } = 0.5;
    public int MaxMergeRequestResponseDays { get; init; } = 7;
    public double MergeRequestResponseWeight { get; init; } = 0.5;
}
