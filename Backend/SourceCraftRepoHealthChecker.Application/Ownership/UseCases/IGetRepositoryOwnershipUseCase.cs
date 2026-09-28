using SourceCraftRepoHealthChecker.Application.Ownership.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.Ownership.UseCases;

public interface IGetRepositoryOwnershipUseCase
{
    public Task<SourceCraftResult<RepositoryOwnership>> GetAsync(
        string repositoryId,
        CancellationToken cancellationToken);
}
