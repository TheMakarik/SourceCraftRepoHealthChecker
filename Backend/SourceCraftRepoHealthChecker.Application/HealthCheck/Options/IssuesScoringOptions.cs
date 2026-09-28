namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

public sealed class IssuesScoringOptions
{
    public required int StaleIssueAgeDays { get; init; }
    public required int MaxFirstResponseDays { get; init; }
    public required int MaxCloseDays { get; init; }
    public required int MaxOpenIssues { get; init; }
}
