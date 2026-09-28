using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.UseCases;

public sealed class GetRepositoryTreeUseCase(ISourceCraftTreeSource treeSource) : IGetRepositoryTreeUseCase
{
    public Task<SourceCraftResult<RepositoryTree>> GetAsync(
        string repositoryId,
        string? path,
        bool recursive,
        CancellationToken cancellationToken) =>
        treeSource.GetTreeAsync(repositoryId, path ?? string.Empty, recursive, cancellationToken);
}
