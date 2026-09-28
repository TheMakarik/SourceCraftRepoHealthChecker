namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record RepositoryFileContent(
    string Path,
    string Content,
    string Language,
    bool Truncated,
    bool Binary);
