using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftActivitySource
{
    public Task<CommitActivity> GetCommitActivityAsync(string repositoryId, CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<Contributor>> GetContributorsAsync(string repositoryId, CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<ReleaseInfo>> GetReleasesAsync(string repositoryId, CancellationToken cancellationToken);
}
