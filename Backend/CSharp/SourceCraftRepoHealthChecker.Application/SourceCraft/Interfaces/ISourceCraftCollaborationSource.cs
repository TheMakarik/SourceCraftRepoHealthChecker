using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftCollaborationSource
{
    public Task<SourceCraftResult<IReadOnlyCollection<MergeRequestInfo>>> GetMergeRequestsAsync(string repositoryId, CancellationToken cancellationToken);

    public Task<SourceCraftResult<IReadOnlyCollection<IssueInfo>>> GetIssuesAsync(string repositoryId, CancellationToken cancellationToken);
}
