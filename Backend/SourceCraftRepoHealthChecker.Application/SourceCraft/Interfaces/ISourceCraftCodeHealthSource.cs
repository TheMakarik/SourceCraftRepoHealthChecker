using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftCodeHealthSource
{
    public Task<SourceCraftResult<CodeHealthReport>> GetCodeHealthAsync(string repositoryId, CancellationToken cancellationToken);
}
