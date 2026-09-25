using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftSecurityAdapter(SourceCraftHttpClient client) : ISourceCraftSecuritySource
{
    public async Task<SourceCraftResult<IReadOnlyCollection<SecurityFinding>>> GetFindingsAsync(string repositoryId, CancellationToken cancellationToken) =>
        (await client.GetAsync<IReadOnlyCollection<SecurityFinding>>($"/repositories/{Uri.EscapeDataString(repositoryId)}/security/findings", cancellationToken)).ToResult();
}
