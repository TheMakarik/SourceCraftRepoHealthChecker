using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftRepositoryCatalog
{
    public Task<SourceCraftResult<IReadOnlyCollection<SourceCraftRepository>>> GetOpenRepositoriesAsync(CancellationToken cancellationToken);

    public Task<SourceCraftResult<SourceCraftRepository>> GetRepositoryAsync(string repositoryId, CancellationToken cancellationToken);
}
