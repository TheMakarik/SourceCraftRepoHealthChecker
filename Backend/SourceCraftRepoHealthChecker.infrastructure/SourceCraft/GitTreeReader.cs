using LibGit2Sharp;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal static class GitTreeReader
{
    public static IReadOnlyList<string> ListTrackedPaths(Repository repository)
    {
        var head = repository.Head.Tip;
        if (head is null)
            return [];

        var paths = new List<string>();
        CollectTrackedPaths(head.Tree, "", paths);
        return paths;
    }

    public static IEnumerable<(string Path, Blob Blob)> EnumerateBlobs(Repository repository)
    {
        var head = repository.Head.Tip;
        if (head is null)
            yield break;

        foreach (var entry in EnumerateBlobs(head.Tree, ""))
            yield return entry;
    }

    private static void CollectTrackedPaths(Tree tree, string prefix, ICollection<string> paths)
    {
        foreach (var entry in tree)
        {
            var path = prefix.Length == 0 ? entry.Name : $"{prefix}/{entry.Name}";
            switch (entry.TargetType)
            {
                case TreeEntryTargetType.Tree when entry.Target is Tree child:
                    CollectTrackedPaths(child, path, paths);
                    break;
                case TreeEntryTargetType.Blob:
                case TreeEntryTargetType.GitLink:
                    paths.Add(path);
                    break;
            }
        }
    }

    private static IEnumerable<(string Path, Blob Blob)> EnumerateBlobs(Tree tree, string prefix)
    {
        foreach (var entry in tree)
        {
            var path = prefix.Length == 0 ? entry.Name : $"{prefix}/{entry.Name}";
            switch (entry.TargetType)
            {
                case TreeEntryTargetType.Tree when entry.Target is Tree child:
                    foreach (var item in EnumerateBlobs(child, path))
                        yield return item;
                    break;
                case TreeEntryTargetType.Blob when entry.Target is Blob blob:
                    yield return (path, blob);
                    break;
            }
        }
    }
}
