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

        var refreshed = await refreshRepositoriesUseCase.RefreshAsync(cancellationToken);
        if (options.MaxRepositoriesPerRun <= 0)
        {
            logger.LogWarning("MaxRepositoriesPerRun is {Maximum}, skipping enqueue", options.MaxRepositoriesPerRun);
            return new ScheduledAnalysisSummary(refreshed, 0);
        }

        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddMinutes(-options.AnalysisIntervalMinutes);
        var repositoryIds = await dbContext.Repositories
            .Where(repository => repository.AnalyzedAt == null || repository.AnalyzedAt < cutoff)
            .OrderBy(repository => repository.AnalyzedAt)
            .Take(options.MaxRepositoriesPerRun)
            .Select(repository => repository.SourceCraftId)
            .ToListAsync(cancellationToken);

        foreach (var repositoryId in repositoryIds)
            await analysisQueue.EnqueueAsync(new AnalysisJob(repositoryId), cancellationToken);

        logger.LogInformation("Scheduled enqueue finished: refreshed {Refreshed}, enqueued {Enqueued}", refreshed, repositoryIds.Count);

        return new ScheduledAnalysisSummary(refreshed, repositoryIds.Count);
    }
}
