using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.UseCases;

public sealed class GetRepositoryFileUseCase(IRepositoryContentSource contentSource) : IGetRepositoryFileUseCase
{
    public Task<SourceCraftResult<RepositoryFileContent>> GetAsync(
        string repositoryId,
        string path,
        CancellationToken cancellationToken) =>
        contentSource.GetFileAsync(repositoryId, path, cancellationToken);
}
