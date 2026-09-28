namespace SourceCraftRepoHealthChecker.Application.Integrity.Models;

public sealed record RepositoryIntegrity(
    string Status,
    int Score,
    IReadOnlyList<string> Signals);
