using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftStructureAdapter(SourceCraftHttpClient client) : ISourceCraftStructureSource
{
    public async Task<SourceCraftResult<RepositoryStructureReport>> GetStructureAsync(string repositoryId, CancellationToken cancellationToken) =>
        (await client.GetAsync<RepositoryStructureReport>($"/repositories/{Uri.EscapeDataString(repositoryId)}/structure", cancellationToken)).ToResult();
}
