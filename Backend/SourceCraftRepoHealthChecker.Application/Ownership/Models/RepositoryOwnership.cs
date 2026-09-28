namespace SourceCraftRepoHealthChecker.Application.Ownership.Models;

public sealed record RepositoryOwnership(
    IReadOnlyList<OwnerStat> Owners,
    int BusFactor,
    int TotalContributors);
