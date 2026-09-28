using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.UseCases;

public interface IGetRepositoryFileUseCase
{
    public Task<SourceCraftResult<RepositoryFileContent>> GetAsync(
        string repositoryId,
        string path,
        CancellationToken cancellationToken);
}
