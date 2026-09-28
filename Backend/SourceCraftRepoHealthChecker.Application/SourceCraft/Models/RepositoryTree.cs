namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record RepositoryTree(
    IReadOnlyList<RepositoryTreeEntry> Entries,
    bool Truncated);
