using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftCollaborationSource
{
    public Task<IReadOnlyCollection<MergeRequestInfo>> GetMergeRequestsAsync(string repositoryId, CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<IssueInfo>> GetIssuesAsync(string repositoryId, CancellationToken cancellationToken);
}
