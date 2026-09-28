using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.UseCases;

public interface IGetRepositoryFoldersUseCase
{
    public Task<SourceCraftResult<IReadOnlyList<RepositoryFolderAnalysis>>> GetAsync(
        string repositoryId,
        CancellationToken cancellationToken);
}
