using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;

namespace SourceCraftRepoHealthChecker.Application.Scheduling;

public sealed class ScheduledAnalysisRunner(
    IRefreshRepositoriesUseCase refreshRepositoriesUseCase,
    IAnalysisQueue analysisQueue,
    ISchedulerLease schedulerLease,
    IRepoHealthCheckerDbContext dbContext,
    IOptions<SchedulingOptions> schedulingOptions,
    IOptions<ScalingOptions> scalingOptions,
    TimeProvider timeProvider,
    ILogger<ScheduledAnalysisRunner> logger) : IScheduledAnalysisRunner
{
    public async Task<ScheduledAnalysisSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        var options = schedulingOptions.Value;
        if (!options.Enabled || !scalingOptions.Value.SchedulerEnabled)
        {
            logger.LogInformation("Scheduled analysis is disabled");
            return new ScheduledAnalysisSummary(0, 0);
        }

        await using var lease = await schedulerLease.AcquireAsync(cancellationToken);
        if (lease is null)
        {
            logger.LogInformation("Scheduler lease is held by another replica, skipping this tick");
            return new ScheduledAnalysisSummary(0, 0);
        }

        var refreshed = await RefreshWithoutBlockingEnqueueAsync(cancellationToken);

        var repositoriesPerWorker = options.MaxRepositoriesPerRun;
        var batchSize = repositoriesPerWorker <= 0
            ? 0
            : repositoriesPerWorker * Math.Max(1, scalingOptions.Value.WorkerConcurrency);
        if (batchSize <= 0)
        {
            logger.LogWarning("MaxRepositoriesPerRun is {Maximum}, skipping enqueue", repositoriesPerWorker);
            return new ScheduledAnalysisSummary(refreshed, 0);
        }

        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddMinutes(-options.AnalysisIntervalMinutes);
        var repositoryIds = await dbContext.Repositories
            // Only open repositories are re-analyzed on schedule: a private one is re-analyzed by its owner
            // with their own token, never with the service token in the background.
            .Where(repository => !repository.IsPrivate)
            .Where(repository => repository.AnalyzedAt == null || repository.AnalyzedAt < cutoff)
            .OrderBy(repository => repository.AnalyzedAt)
            .Take(batchSize)
            .Select(repository => repository.SourceCraftId)
            .ToListAsync(cancellationToken);

        foreach (var repositoryId in repositoryIds)
            await analysisQueue.EnqueueAsync(new AnalysisJob(repositoryId), cancellationToken);

        logger.LogInformation("Scheduled enqueue finished: refreshed {Refreshed}, enqueued {Enqueued}", refreshed, repositoryIds.Count);

        return new ScheduledAnalysisSummary(refreshed, repositoryIds.Count);
    }

    private async Task<int> RefreshWithoutBlockingEnqueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await refreshRepositoriesUseCase.RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Repository catalog refresh failed; enqueueing already-known repositories");
            return 0;
        }
    }
}
