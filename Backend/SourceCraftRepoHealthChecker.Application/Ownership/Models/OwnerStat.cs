namespace SourceCraftRepoHealthChecker.Application.Ownership.Models;

public sealed record OwnerStat(
    string Login,
    int Commits,
    int FilesTouched,
    IReadOnlyList<string> TopDirectories);
