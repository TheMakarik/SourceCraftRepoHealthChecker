using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Scheduling;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.Scheduling;

public sealed class ScheduledAnalysisBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SchedulingOptions> schedulingOptions,
    ILogger<ScheduledAnalysisBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = schedulingOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("Scheduled analysis background service is disabled");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, options.RepositoriesRefreshMinutes));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IScheduledAnalysisRunner>();
                var summary = await runner.RunOnceAsync(stoppingToken);
                logger.LogInformation("Scheduled analysis completed: refreshed {Refreshed}, enqueued {Enqueued}", summary.Refreshed, summary.Enqueued);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled analysis run failed");
            }
        }
    }
}
