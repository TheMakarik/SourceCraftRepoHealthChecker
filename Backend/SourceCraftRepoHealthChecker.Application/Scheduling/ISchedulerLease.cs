namespace SourceCraftRepoHealthChecker.Application.Scheduling;

public interface ISchedulerLease
{
    public Task<IAsyncDisposable?> AcquireAsync(CancellationToken cancellationToken);
}
