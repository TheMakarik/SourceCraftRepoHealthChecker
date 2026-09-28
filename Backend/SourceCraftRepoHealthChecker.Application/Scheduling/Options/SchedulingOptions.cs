namespace SourceCraftRepoHealthChecker.Application.Scheduling.Options;

public sealed class SchedulingOptions
{
    public required bool Enabled { get; init; }
    public required int RepositoriesRefreshMinutes { get; init; }
    public required int AnalysisIntervalMinutes { get; init; }
    public required int MaxRepositoriesPerRun { get; init; }
}
