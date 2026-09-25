using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftActivityAdapter(SourceCraftHttpClient client) : ISourceCraftActivitySource
{
    public async Task<SourceCraftResult<CommitActivity>> GetCommitActivityAsync(string repositoryId, CancellationToken cancellationToken) =>
        (await client.GetAsync<CommitActivity>($"/repositories/{Uri.EscapeDataString(repositoryId)}/activity/commits", cancellationToken)).ToResult();

    public async Task<SourceCraftResult<IReadOnlyCollection<Contributor>>> GetContributorsAsync(string repositoryId, CancellationToken cancellationToken) =>
        (await client.GetAsync<IReadOnlyCollection<Contributor>>($"/repositories/{Uri.EscapeDataString(repositoryId)}/activity/contributors", cancellationToken)).ToResult();

    public async Task<SourceCraftResult<IReadOnlyCollection<ReleaseInfo>>> GetReleasesAsync(string repositoryId, CancellationToken cancellationToken) =>
        (await client.GetAsync<IReadOnlyCollection<ReleaseInfo>>($"/repositories/{Uri.EscapeDataString(repositoryId)}/activity/releases", cancellationToken)).ToResult();
}
