namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class ActivityScoringOptions
{
    public required int ActiveWithinDays { get; init; }
    public required int StaleAfterDays { get; init; }
    public required int CommitFrequencyForFullScore { get; init; }
    public required int ContributorsForFullScore { get; init; }
    public required int ReleasesForFullScore { get; init; }
}
