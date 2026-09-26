namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class AnomalyDetectionOptions
{
    public required int CommitBurstThreshold { get; init; }
    public required int CommitBurstWindowDays { get; init; }
    public required int MergeRequestBurstThreshold { get; init; }
    public required int MergeRequestBurstWindowDays { get; init; }
    public required int NewContributorRecentDays { get; init; }
    public required int InactiveAuthorDays { get; init; }
    public required int SmallChangeCommitThreshold { get; init; }
    public required int SmallChangeRecentDays { get; init; }
}
