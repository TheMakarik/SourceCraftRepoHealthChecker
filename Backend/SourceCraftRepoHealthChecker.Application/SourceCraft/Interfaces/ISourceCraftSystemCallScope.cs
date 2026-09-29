namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftSystemCallScope
{
    public bool IsSystemCall { get; }

    public IDisposable Begin();
}
