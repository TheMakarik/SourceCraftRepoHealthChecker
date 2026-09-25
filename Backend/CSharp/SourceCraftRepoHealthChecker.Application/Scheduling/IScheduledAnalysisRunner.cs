namespace SourceCraftRepoHealthChecker.Application.Scheduling;

public interface IScheduledAnalysisRunner
{
    public Task<ScheduledAnalysisSummary> RunOnceAsync(CancellationToken cancellationToken);
}
