using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.UseCases;

public interface IGetRepositoryTreeUseCase
{
    public Task<SourceCraftResult<RepositoryTree>> GetAsync(
        string repositoryId,
        string? path,
        bool recursive,
        CancellationToken cancellationToken);
}
