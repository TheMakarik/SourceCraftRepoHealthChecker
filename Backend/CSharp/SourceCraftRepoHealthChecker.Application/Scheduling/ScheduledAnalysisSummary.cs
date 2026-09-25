namespace SourceCraftRepoHealthChecker.Application.Scheduling;

public sealed record ScheduledAnalysisSummary(int Refreshed, int Analyzed, int Failed);
