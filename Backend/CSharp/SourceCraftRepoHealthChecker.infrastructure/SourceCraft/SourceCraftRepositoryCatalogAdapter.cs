using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftRepositoryCatalogAdapter(
    SourceCraftHttpClient client,
    IOptions<SourceCraftServiceOptions> options) : ISourceCraftRepositoryCatalog
{
    public async Task<SourceCraftResult<IReadOnlyCollection<SourceCraftRepository>>> GetOpenRepositoriesAsync(CancellationToken cancellationToken)
    {
        var repositories = new List<SourceCraftRepository>();
        string? pageToken = null;

        do
        {
            var path = $"/repositories?pageSize={options.Value.PageSize}";
            if (!string.IsNullOrEmpty(pageToken))
                path += $"&pageToken={Uri.EscapeDataString(pageToken)}";

            var page = await client.GetAsync<IReadOnlyCollection<SourceCraftRepository>>(path, cancellationToken);
            if (page.Status != DataStatus.Available)
                return new SourceCraftResult<IReadOnlyCollection<SourceCraftRepository>>(
                    repositories.Count > 0 ? DataStatus.Available : page.Status, repositories, page.Reason);

            if (page.Data is not null)
                repositories.AddRange(page.Data);

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return new SourceCraftResult<IReadOnlyCollection<SourceCraftRepository>>(
            repositories.Count == 0 ? DataStatus.NoData : DataStatus.Available, repositories, null);
    }

    public async Task<SourceCraftResult<SourceCraftRepository>> GetRepositoryAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var page = await client.GetAsync<SourceCraftRepository>($"/repositories/{Uri.EscapeDataString(repositoryId)}", cancellationToken);
        return page.ToResult();
    }
}
