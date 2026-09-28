namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record RepositoryFolderAnalysis(
    string Path,
    int Files,
    int TodoCount,
    int FixmeCount,
    bool Readme,
    bool License,
    bool Tests);
