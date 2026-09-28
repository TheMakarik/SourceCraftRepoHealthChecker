namespace SourceCraftRepoHealthChecker.Application.Scheduling.Options;

public sealed class ScalingOptions
{
    public required bool SchedulerEnabled { get; init; }
    public required bool WorkerEnabled { get; init; }
    public required int WorkerConcurrency { get; init; }
    public required int WorkerMaxAttempts { get; init; }
    public required int WorkerRetryDelayMilliseconds { get; init; }
    public required int QueueCapacity { get; init; }
}
