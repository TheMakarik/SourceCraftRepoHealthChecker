namespace SourceCraftRepoHealthChecker.infrastructure.Analysis;

internal sealed class AnalysisStatusSubscription(Action unsubscribe) : IDisposable
{
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            unsubscribe();
    }
}
