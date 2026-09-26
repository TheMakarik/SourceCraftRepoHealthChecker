using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Scheduling;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;

namespace SourceCraftRepoHealthChecker.UnitTests.Scheduling;

public sealed class ChannelAnalysisQueueTests
{
    [Fact]
    public async Task DequeueAsync_WhenEnqueued_ReturnsJobsInOrder()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(capacity: 10);

        // Act
        await systemUnderTests.EnqueueAsync(new AnalysisJob("repo-1"), CancellationToken.None);
        await systemUnderTests.EnqueueAsync(new AnalysisJob("repo-2"), CancellationToken.None);
        var first = await systemUnderTests.DequeueAsync(CancellationToken.None);
        var second = await systemUnderTests.DequeueAsync(CancellationToken.None);

        // Assert
        first.RepositoryId.Should().Be("repo-1");
        second.RepositoryId.Should().Be("repo-2");
    }

    [Fact]
    public async Task EnqueueAsync_WhenCapacityReached_WaitsUntilDequeued()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(capacity: 1);
        await systemUnderTests.EnqueueAsync(new AnalysisJob("repo-1"), CancellationToken.None);

        // Act
        var pending = systemUnderTests.EnqueueAsync(new AnalysisJob("repo-2"), CancellationToken.None).AsTask();
        await Task.Delay(50);
        var blockedWhileFull = pending.IsCompleted;

        var dequeued = await systemUnderTests.DequeueAsync(CancellationToken.None);
        await pending;

        // Assert
        blockedWhileFull.Should().BeFalse();
        dequeued.RepositoryId.Should().Be("repo-1");
    }

    private static ChannelAnalysisQueue CreateSystemUnderTests(int capacity)
    {
        var options = Options.Create(new ScalingOptions
        {
            SchedulerEnabled = true,
            WorkerEnabled = true,
            WorkerConcurrency = 1,
            WorkerMaxAttempts = 1,
            WorkerRetryDelayMilliseconds = 0,
            QueueCapacity = capacity
        });

        return new ChannelAnalysisQueue(options);
    }
}
