using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Scheduling;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.Scheduling;

public sealed class AnalysisWorkerBackgroundService(
    IAnalysisQueue analysisQueue,
    IServiceScopeFactory scopeFactory,
    IOptions<ScalingOptions> scalingOptions,
    ILogger<AnalysisWorkerBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = scalingOptions.Value;
        if (!options.WorkerEnabled)
        {
            logger.LogInformation("Analysis worker is disabled");
            return;
        }

        using var semaphore = new SemaphoreSlim(Math.Max(1, options.WorkerConcurrency), Math.Max(1, options.WorkerConcurrency));
        var running = new List<Task>();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var job = await analysisQueue.DequeueAsync(stoppingToken);
                await semaphore.WaitAsync(stoppingToken);
                running.Add(ProcessAsync(job, semaphore, stoppingToken));
                running.RemoveAll(task => task.IsCompleted);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await Task.WhenAll(running);
        }
    }

    private async Task ProcessAsync(AnalysisJob job, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var worker = scope.ServiceProvider.GetRequiredService<AnalysisWorker>();
            await worker.ProcessAsync(job, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            semaphore.Release();
        }
    }
}
