using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftSecuritySource
{
    public Task<IReadOnlyCollection<SecurityFinding>> GetFindingsAsync(string repositoryId, CancellationToken cancellationToken);
}
