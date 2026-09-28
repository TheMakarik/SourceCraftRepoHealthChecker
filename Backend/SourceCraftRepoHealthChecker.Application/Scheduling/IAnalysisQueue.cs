namespace SourceCraftRepoHealthChecker.Application.Scheduling;

public interface IAnalysisQueue
{
    public ValueTask EnqueueAsync(AnalysisJob job, CancellationToken cancellationToken);

    public ValueTask<AnalysisJob> DequeueAsync(CancellationToken cancellationToken);
}
