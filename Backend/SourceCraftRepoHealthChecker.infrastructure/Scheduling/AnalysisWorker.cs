using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Scheduling;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.Scheduling;

public sealed class AnalysisWorker(
    IAnalyzeRepositoryUseCase analyzeRepositoryUseCase,
    IRepoHealthCheckerDbContext dbContext,
    ISourceCraftSystemCallScope systemCallScope,
    IOptions<ScalingOptions> scalingOptions,
    TimeProvider timeProvider,
    ILogger<AnalysisWorker> logger)
{
    private const int MaxErrorMessageLength = 4000;

    public async Task<bool> ProcessAsync(AnalysisJob job, CancellationToken cancellationToken)
    {
        using var systemScope = systemCallScope.Begin();
        var options = scalingOptions.Value;
        var maxAttempts = Math.Max(1, options.WorkerMaxAttempts);
        var delay = TimeSpan.FromMilliseconds(Math.Max(0, options.WorkerRetryDelayMilliseconds));

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await analyzeRepositoryUseCase.AnalyzeAsync(new AnalyzeRepositoryRequest(job.RepositoryId, job.RequestedByUserId), cancellationToken);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Analysis attempt {Attempt}/{MaxAttempts} failed for {RepositoryId}", attempt, maxAttempts, job.RepositoryId);

                if (attempt >= maxAttempts)
                {
                    logger.LogError(exception, "Analysis failed for {RepositoryId} after {MaxAttempts} attempts", job.RepositoryId, maxAttempts);
                    await RecordFailureAsync(job, exception, cancellationToken);
                    return false;
                }

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay * attempt, cancellationToken);
            }
        }

        return false;
    }

    private async Task RecordFailureAsync(AnalysisJob job, Exception exception, CancellationToken cancellationToken)
    {
        var repositories = await dbContext.Repositories
            .Where(item => item.SourceCraftId == job.RepositoryId)
            .ToListAsync(cancellationToken);
        var repository = repositories.FirstOrDefault();
        if (repository is null)
            return;

        var runs = await dbContext.AnalysisRuns
            .Where(item => item.RepositoryId == repository.Id)
            .ToListAsync(cancellationToken);
        var latestRun = runs
            .OrderByDescending(item => item.CompletedAt ?? item.StartedAt)
            .FirstOrDefault();

        if (latestRun is { Status: AnalysisStatus.Failed })
            return;

        var now = timeProvider.GetUtcNow();
        var reason = Truncate(exception.Message, MaxErrorMessageLength);

        if (latestRun is { Status: AnalysisStatus.Running })
        {
            latestRun.Status = AnalysisStatus.Failed;
            latestRun.DataStatus = DataStatus.NoData;
            latestRun.CompletedAt = now;
            latestRun.ErrorMessage = reason;
        }
        else
        {
            dbContext.AnalysisRuns.Add(new AnalysisRun
            {
                Id = Guid.NewGuid(),
                RepositoryId = repository.Id,
                UserId = job.RequestedByUserId,
                Status = AnalysisStatus.Failed,
                DataStatus = DataStatus.NoData,
                StartedAt = now,
                CompletedAt = now,
                ErrorMessage = reason
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
