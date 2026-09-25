namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public sealed class TempFileSystem : IDisposable
{
    public string Path { get; }

    public TempFileSystem()
    {
        Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), $"SourceCraftRepoHealthChecker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

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
