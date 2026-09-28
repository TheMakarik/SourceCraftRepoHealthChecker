using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ownership.Interfaces;
using SourceCraftRepoHealthChecker.Application.Ownership.Models;
using SourceCraftRepoHealthChecker.Application.Ownership.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ownership.UseCases;

public sealed class GetRepositoryOwnershipUseCase(
    IOwnershipSource ownershipSource,
    IOptions<OwnershipOptions> options) : IGetRepositoryOwnershipUseCase
{
    public async Task<SourceCraftResult<RepositoryOwnership>> GetAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var result = await ownershipSource.GetOwnersAsync(repositoryId, cancellationToken);
        if (result.Status != DataStatus.Available || result.Data is null)
            return new SourceCraftResult<RepositoryOwnership>(result.Status, null, result.Reason);

        var owners = result.Data
            .OrderByDescending(owner => owner.Commits)
            .ThenBy(owner => owner.Login, StringComparer.Ordinal)
            .ToArray();

        var busFactor = CalculateBusFactor(owners, options.Value.CoverageThresholdPercent);
        var ownership = new RepositoryOwnership(owners, busFactor, owners.Length);
        return new SourceCraftResult<RepositoryOwnership>(DataStatus.Available, ownership, null);
    }

    private static int CalculateBusFactor(IReadOnlyList<OwnerStat> owners, int coverageThresholdPercent)
    {
        if (owners.Count == 0)
            return 0;

        var totalCommits = 0L;
        foreach (var owner in owners)
            totalCommits += owner.Commits;

        if (totalCommits == 0)
            return 1;

        var threshold = Math.Clamp(coverageThresholdPercent, 1, 100);
        var requiredCommits = totalCommits * threshold / 100.0;

        var coveredCommits = 0L;
        for (var index = 0; index < owners.Count; index++)
        {
            coveredCommits += owners[index].Commits;
            if (coveredCommits >= requiredCommits)
                return index + 1;
        }

        return owners.Count;
    }
}
