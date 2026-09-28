using SourceCraftRepoHealthChecker.Application.Integrity.Models;

namespace SourceCraftRepoHealthChecker.Application.Integrity.UseCases;

public interface IComputeRepositoryIntegrityUseCase
{
    public Task<RepositoryIntegrity?> GetAsync(string sourceCraftId, CancellationToken cancellationToken);
}
