using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftTreeSource
{
    public Task<SourceCraftResult<RepositoryTree>> GetTreeAsync(
        string repositoryId,
        string path,
        bool recursive,
        CancellationToken cancellationToken);
}
