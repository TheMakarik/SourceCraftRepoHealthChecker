using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Scheduling;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.Scheduling;

public sealed class AnalysisWorker(
    IAnalyzeRepositoryUseCase analyzeRepositoryUseCase,
    IOptions<ScalingOptions> scalingOptions,
    ILogger<AnalysisWorker> logger)
{
    public async Task<bool> ProcessAsync(AnalysisJob job, CancellationToken cancellationToken)
    {
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
                    return false;
                }

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay * attempt, cancellationToken);
            }
        }

        return false;
    }
}
