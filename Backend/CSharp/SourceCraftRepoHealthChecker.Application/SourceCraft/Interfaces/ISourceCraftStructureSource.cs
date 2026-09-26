using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftStructureSource
{
    public Task<SourceCraftResult<RepositoryStructureReport>> GetStructureAsync(string repositoryId, CancellationToken cancellationToken);
}
