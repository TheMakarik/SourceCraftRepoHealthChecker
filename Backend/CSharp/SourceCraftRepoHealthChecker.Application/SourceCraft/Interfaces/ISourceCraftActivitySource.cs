using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftActivitySource
{
    public Task<SourceCraftResult<CommitActivity>> GetCommitActivityAsync(string repositoryId, CancellationToken cancellationToken);

    public Task<SourceCraftResult<IReadOnlyCollection<Contributor>>> GetContributorsAsync(string repositoryId, CancellationToken cancellationToken);

    public Task<SourceCraftResult<IReadOnlyCollection<ReleaseInfo>>> GetReleasesAsync(string repositoryId, CancellationToken cancellationToken);
}
