using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftPipelineSource
{
    public Task<IReadOnlyCollection<PipelineRun>> GetPipelineRunsAsync(string repositoryId, CancellationToken cancellationToken);
}
