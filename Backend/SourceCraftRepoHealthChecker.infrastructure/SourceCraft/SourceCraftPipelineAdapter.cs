using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftPipelineAdapter(
    ISourceCraftApi api,
    IOptions<SourceCraftServiceOptions> options) : ISourceCraftPipelineSource
{
    public async Task<SourceCraftResult<IReadOnlyCollection<PipelineRun>>> GetPipelineRunsAsync(string repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var runs = await SourceCraftPagination.CollectAsync(
                settings.MaxItems,
                settings.MaxPages,
                (pageToken, token) => FetchPipelineRunsAsync(repositoryId, pageToken, token),
                cancellationToken);

            List<PipelineRun> pipelineRuns = [];
            foreach (var run in runs)
            {
                var startedAt = run.Dates?.StartedAt ?? run.Dates?.CreatedAt;
                if (startedAt is null)
                    continue;

                var id = string.IsNullOrEmpty(run.Id) ? run.Slug ?? string.Empty : run.Id;
                pipelineRuns.Add(new PipelineRun(
                    id,
                    MapStatus(run.Status),
                    string.Empty,
                    startedAt.Value,
                    run.Dates?.FinishedAt));
            }

            return new SourceCraftResult<IReadOnlyCollection<PipelineRun>>(DataStatus.Available, pipelineRuns, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return SourceCraftFailure.Unavailable<IReadOnlyCollection<PipelineRun>>(exception);
        }
    }

    private async Task<(IReadOnlyCollection<PipelineRunDto> Items, string? NextPageToken)> FetchPipelineRunsAsync(
        string repositoryId,
        string? pageToken,
        CancellationToken cancellationToken)
    {
        var page = await api.GetPipelineRunsAsync(repositoryId, options.Value.PageSize, pageToken, cancellationToken);
        return (page.Runs ?? [], page.NextPageToken);
    }

    private static PipelineStatus MapStatus(string? status) => status switch
    {
        "success" => PipelineStatus.Success,
        "failed" or "timeout" or "rejected" => PipelineStatus.Failed,
        "canceled" => PipelineStatus.Canceled,
        "skipped" => PipelineStatus.Skipped,
        _ => PipelineStatus.Running
    };
}
