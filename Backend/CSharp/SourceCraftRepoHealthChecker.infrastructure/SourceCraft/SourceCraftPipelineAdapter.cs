using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftPipelineAdapter(SourceCraftHttpClient client) : ISourceCraftPipelineSource
{
    public async Task<SourceCraftResult<IReadOnlyCollection<PipelineRun>>> GetPipelineRunsAsync(string repositoryId, CancellationToken cancellationToken) =>
        (await client.GetAsync<IReadOnlyCollection<PipelineRun>>($"/repositories/{Uri.EscapeDataString(repositoryId)}/pipelines", cancellationToken)).ToResult();
}
