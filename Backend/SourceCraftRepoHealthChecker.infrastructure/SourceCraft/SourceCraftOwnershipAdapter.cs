using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ownership.Interfaces;
using SourceCraftRepoHealthChecker.Application.Ownership.Models;
using SourceCraftRepoHealthChecker.Application.Ownership.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftOwnershipAdapter(
    GitWorkingCopyProvider workingCopyProvider,
    IOptions<OwnershipOptions> options) : IOwnershipSource
{
    public async Task<SourceCraftResult<IReadOnlyList<OwnerStat>>> GetOwnersAsync(
        string repositoryId,
        CancellationToken cancellationToken)
    {
        var clone = await workingCopyProvider.AcquireAsync(repositoryId, cancellationToken);
        var workingCopy = clone.Data;
        if (clone.Status != DataStatus.Available || workingCopy is null)
            return new SourceCraftResult<IReadOnlyList<OwnerStat>>(clone.Status, null, clone.Reason);

        using (workingCopy)
        {
            var owners = await Task.Run(
                () => GitOwnershipReader.ReadOwners(workingCopy.Path, options.Value.TopDirectoriesCount, cancellationToken),
                cancellationToken);
            if (owners.Count == 0)
                return SourceCraftFailure.NoData<IReadOnlyList<OwnerStat>>("repository has no commits");

            return new SourceCraftResult<IReadOnlyList<OwnerStat>>(DataStatus.Available, owners, null);
        }
    }
}
