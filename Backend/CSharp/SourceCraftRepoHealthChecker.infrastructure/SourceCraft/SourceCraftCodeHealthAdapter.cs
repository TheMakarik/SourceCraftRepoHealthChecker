using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftCodeHealthAdapter(SourceCraftHttpClient client) : ISourceCraftCodeHealthSource
{
    public async Task<SourceCraftResult<CodeHealthReport>> GetCodeHealthAsync(string repositoryId, CancellationToken cancellationToken) =>
        (await client.GetAsync<CodeHealthReport>($"/repositories/{Uri.EscapeDataString(repositoryId)}/code-health", cancellationToken)).ToResult();
}
