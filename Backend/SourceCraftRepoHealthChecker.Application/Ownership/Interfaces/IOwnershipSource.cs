using SourceCraftRepoHealthChecker.Application.Ownership.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.Ownership.Interfaces;

public interface IOwnershipSource
{
    public Task<SourceCraftResult<IReadOnlyList<OwnerStat>>> GetOwnersAsync(
        string repositoryId,
        CancellationToken cancellationToken);
}
