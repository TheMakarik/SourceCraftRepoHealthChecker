namespace SourceCraftRepoHealthChecker.Application.Integrity.Options;

public sealed class RepositoryIntegrityOptions
{
    public int MaximumScore { get; init; } = 100;
    public int MinimumScore { get; init; } = 0;
    public int OkMinimumScore { get; init; } = 85;
    public int SuspiciousMaximumScore { get; init; } = 60;
    public int ActivityAnomalyPenalty { get; init; } = 20;
    public int EmptyActivityPenalty { get; init; } = 25;
    public int TinyCommitsPenalty { get; init; } = 10;
    public int LikesWithoutActivityPenalty { get; init; } = 30;
    public int SuspicionLikesMinimum { get; init; } = 10;
    public int InactivityDays { get; init; } = 180;
    public int TinyCommitCountThreshold { get; init; } = 2;
    public int TinyCommitMinimumContributors { get; init; } = 3;
    public double TinyCommitContributorRatio { get; init; } = 0.6;
}
