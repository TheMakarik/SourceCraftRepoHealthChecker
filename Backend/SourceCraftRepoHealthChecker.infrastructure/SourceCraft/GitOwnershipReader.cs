using LibGit2Sharp;
using SourceCraftRepoHealthChecker.Application.Ownership.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal static class GitOwnershipReader
{
    public static IReadOnlyList<OwnerStat> ReadOwners(string repositoryPath, int topDirectoriesCount, CancellationToken cancellationToken)
    {
        using var repository = new Repository(repositoryPath);
        if (repository.Head.Tip is null)
            return [];

        var byKey = new Dictionary<string, GitOwnerAccumulator>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var commit in repository.Commits)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = commit.Author.Name ?? string.Empty;
            var email = commit.Author.Email ?? string.Empty;
            var key = email.Length == 0 ? "name:" + name : email.ToLowerInvariant();

            if (!byKey.TryGetValue(key, out var accumulator))
            {
                accumulator = new GitOwnerAccumulator(name.Length == 0 ? email : name);
                byKey[key] = accumulator;
                order.Add(key);
            }

            accumulator.Commits++;
            CollectTouchedPaths(repository, commit, accumulator, cancellationToken);
        }

        var owners = new List<OwnerStat>(order.Count);
        foreach (var key in order)
        {
            var accumulator = byKey[key];
            owners.Add(new OwnerStat(
                accumulator.Login,
                accumulator.Commits,
                accumulator.TouchedPaths.Count,
                accumulator.TopDirectories(topDirectoriesCount)));
        }

        return owners;
    }

    private static void CollectTouchedPaths(Repository repository, Commit commit, GitOwnerAccumulator accumulator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parents = commit.Parents.ToList();
        if (parents.Count > 1)
            return;

        Tree? parentTree = parents.Count == 1 ? parents[0].Tree : null;
        var changes = repository.Diff.Compare<TreeChanges>(parentTree, commit.Tree);
        foreach (var change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            accumulator.AddTouchedPath(change.Path);
        }
    }
}
