namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal sealed class SourceCraftSystemCallScopeHandle(SourceCraftSystemCallScope scope, bool previous) : IDisposable
{
    public void Dispose() => scope.Restore(previous);
}
