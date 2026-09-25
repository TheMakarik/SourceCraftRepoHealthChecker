using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftCollaborationAdapter(SourceCraftHttpClient client) : ISourceCraftCollaborationSource
{
    public async Task<SourceCraftResult<IReadOnlyCollection<MergeRequestInfo>>> GetMergeRequestsAsync(string repositoryId, CancellationToken cancellationToken) =>
        (await client.GetAsync<IReadOnlyCollection<MergeRequestInfo>>($"/repositories/{Uri.EscapeDataString(repositoryId)}/merge-requests", cancellationToken)).ToResult();

    public async Task<SourceCraftResult<IReadOnlyCollection<IssueInfo>>> GetIssuesAsync(string repositoryId, CancellationToken cancellationToken) =>
        (await client.GetAsync<IReadOnlyCollection<IssueInfo>>($"/repositories/{Uri.EscapeDataString(repositoryId)}/issues", cancellationToken)).ToResult();
}
