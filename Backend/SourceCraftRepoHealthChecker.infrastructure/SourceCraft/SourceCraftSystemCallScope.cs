using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftSystemCallScope : ISourceCraftSystemCallScope
{
    private readonly AsyncLocal<bool> _isSystemCall = new();

    public bool IsSystemCall => _isSystemCall.Value;

    public IDisposable Begin()
    {
        var previous = _isSystemCall.Value;
        _isSystemCall.Value = true;
        return new SourceCraftSystemCallScopeHandle(this, previous);
    }

    internal void Restore(bool previous) => _isSystemCall.Value = previous;
}
