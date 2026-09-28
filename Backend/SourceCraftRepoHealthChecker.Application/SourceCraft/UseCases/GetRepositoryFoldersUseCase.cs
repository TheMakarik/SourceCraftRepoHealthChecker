using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.UseCases;

public sealed class GetRepositoryFoldersUseCase(IRepositoryContentSource contentSource) : IGetRepositoryFoldersUseCase
{
    public Task<SourceCraftResult<IReadOnlyList<RepositoryFolderAnalysis>>> GetAsync(
        string repositoryId,
        CancellationToken cancellationToken) =>
        contentSource.GetFoldersAsync(repositoryId, cancellationToken);
}
