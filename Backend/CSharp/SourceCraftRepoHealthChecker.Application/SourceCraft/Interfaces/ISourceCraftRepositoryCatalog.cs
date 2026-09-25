using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftRepositoryCatalog
{
    public Task<IReadOnlyCollection<SourceCraftRepository>> GetOpenRepositoriesAsync(CancellationToken cancellationToken);

    public Task<SourceCraftRepository?> GetRepositoryAsync(string repositoryId, CancellationToken cancellationToken);
}
