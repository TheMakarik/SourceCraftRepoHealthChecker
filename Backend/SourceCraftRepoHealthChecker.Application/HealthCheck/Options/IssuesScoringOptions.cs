namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class IssuesScoringOptions
{
    public required int StaleIssueAgeDays { get; init; }
    public required int MaxFirstResponseDays { get; init; }
    public required int MaxCloseDays { get; init; }
    public required int MaxOpenIssues { get; init; }
    public int ClosedIssuesForFullScore { get; init; } = 20;
    public double ClosedWeight { get; init; } = 0.5;
    public int DynamicsWindowDays { get; init; } = 90;
    public double DynamicsWeight { get; init; } = 0.5;
}
