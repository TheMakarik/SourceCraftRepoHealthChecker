namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class GitWorkingCopy(string path, bool deleteOnDispose = true) : IDisposable
{
    public string Path { get; } = path;

    public void Dispose()
    {
        if (!deleteOnDispose)
            return;

        TryDelete(Path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
