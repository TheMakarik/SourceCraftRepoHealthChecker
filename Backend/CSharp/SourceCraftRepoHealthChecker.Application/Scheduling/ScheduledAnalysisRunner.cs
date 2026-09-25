using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;

namespace SourceCraftRepoHealthChecker.Application.Scheduling;

public sealed class ScheduledAnalysisRunner(
    IRefreshRepositoriesUseCase refreshRepositoriesUseCase,
    IAnalyzeRepositoryUseCase analyzeRepositoryUseCase,
    IRepoHealthCheckerDbContext dbContext,
    IOptions<SchedulingOptions> schedulingOptions,
    TimeProvider timeProvider,
    ILogger<ScheduledAnalysisRunner> logger) : IScheduledAnalysisRunner
{
    public async Task<ScheduledAnalysisSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        var options = schedulingOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("Scheduled analysis is disabled");
            return new ScheduledAnalysisSummary(0, 0, 0);
        }

        var refreshed = await refreshRepositoriesUseCase.RefreshAsync(cancellationToken);
        if (options.MaxRepositoriesPerRun <= 0)
        {
            logger.LogWarning("MaxRepositoriesPerRun is {Maximum}, skipping analysis", options.MaxRepositoriesPerRun);
            return new ScheduledAnalysisSummary(refreshed, 0, 0);
        }

        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddMinutes(-options.AnalysisIntervalMinutes);
        var repositories = await dbContext.Repositories
            .Where(repository => repository.AnalyzedAt == null || repository.AnalyzedAt < cutoff)
            .OrderBy(repository => repository.AnalyzedAt)
            .Take(options.MaxRepositoriesPerRun)
            .ToListAsync(cancellationToken);

        var analyzed = 0;
        var failed = 0;
        foreach (var repository in repositories)
        {
            try
            {
                await analyzeRepositoryUseCase.AnalyzeAsync(new AnalyzeRepositoryRequest(repository.SourceCraftId), cancellationToken);
                analyzed++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                logger.LogError(exception, "Failed to analyze repository {RepositoryId}", repository.SourceCraftId);
            }
        }

        logger.LogInformation("Scheduled analysis finished: refreshed {Refreshed}, analyzed {Analyzed}, failed {Failed}", refreshed, analyzed, failed);

        return new ScheduledAnalysisSummary(refreshed, analyzed, failed);
    }
}
