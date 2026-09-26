namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record RepositoryStructureReport(
    int TotalFiles,
    int TotalDirectories,
    int MaxDepth,
    int RootFiles,
    string LargestDirectory,
    int LargestDirectoryFiles);
