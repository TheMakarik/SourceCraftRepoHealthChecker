namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record RepositoryTreeEntry(
    string Name,
    string Path,
    string Type);
