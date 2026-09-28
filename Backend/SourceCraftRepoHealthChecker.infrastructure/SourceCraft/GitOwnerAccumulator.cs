namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal sealed class GitOwnerAccumulator(string login)
{
    public string Login { get; } = login;
    public int Commits { get; set; }
    public HashSet<string> TouchedPaths { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> DirectoryTouches { get; } = new(StringComparer.Ordinal);

    public void AddTouchedPath(string path)
    {
        if (!TouchedPaths.Add(path))
            return;

        var directory = ResolveDirectory(path);
        DirectoryTouches[directory] = DirectoryTouches.GetValueOrDefault(directory) + 1;
    }

    public IReadOnlyList<string> TopDirectories(int count)
    {
        if (count <= 0)
            return [];

        return DirectoryTouches
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(count)
            .Select(pair => pair.Key)
            .ToArray();
    }

    private static string ResolveDirectory(string path)
    {
        var slashIndex = path.LastIndexOf('/');
        return slashIndex < 0 ? "." : path[..slashIndex];
    }
}
