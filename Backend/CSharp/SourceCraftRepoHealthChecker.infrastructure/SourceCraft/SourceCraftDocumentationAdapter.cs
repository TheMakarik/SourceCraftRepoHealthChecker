using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftDocumentationAdapter(SourceCraftHttpClient client) : ISourceCraftDocumentationSource
{
    public async Task<SourceCraftResult<DocumentationReport>> GetDocumentationAsync(string repositoryId, CancellationToken cancellationToken) =>
        (await client.GetAsync<DocumentationReport>($"/repositories/{Uri.EscapeDataString(repositoryId)}/documentation", cancellationToken)).ToResult();
}
