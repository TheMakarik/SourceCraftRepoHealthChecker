using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftPipelineSource
{
    public Task<SourceCraftResult<IReadOnlyCollection<PipelineRun>>> GetPipelineRunsAsync(string repositoryId, CancellationToken cancellationToken);
}
