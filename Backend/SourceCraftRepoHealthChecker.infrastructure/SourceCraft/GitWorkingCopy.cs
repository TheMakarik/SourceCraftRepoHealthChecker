namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class GitWorkingCopy(string path) : IDisposable
{
    public string Path { get; } = path;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
