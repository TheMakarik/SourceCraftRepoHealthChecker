using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Application.Scheduling;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;
using SourceCraftRepoHealthChecker.UnitTests.Scheduling.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.Scheduling;

public sealed class ScheduledAnalysisRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IRefreshRepositoriesUseCase _refreshRepositoriesUseCase = A.Fake<IRefreshRepositoriesUseCase>();
    private readonly IAnalysisQueue _analysisQueue = A.Fake<IAnalysisQueue>();
    private readonly ISchedulerLease _schedulerLease = A.Fake<ISchedulerLease>();
    private readonly IOptions<SchedulingOptions> _schedulingOptions = A.Fake<IOptions<SchedulingOptions>>();
    private readonly ILogger<ScheduledAnalysisRunner> _logger = A.Dummy<ILogger<ScheduledAnalysisRunner>>();
    private readonly TimeProvider _timeProvider = new StubTimeProvider(Now);

    [Fact]
    public async Task RunOnceAsync_WhenEnabled_RefreshesAndEnqueuesStaleRepositories()
    {
        // Arrange
        GrantLease();
        A.CallTo(() => _refreshRepositoriesUseCase.RefreshAsync(A<CancellationToken>._)).Returns(7);
        var systemUnderTests = CreateSystemUnderTests(CreateRepositories(2), enabled: true, schedulerEnabled: true, maxRepositoriesPerRun: 5);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Should().Be(new ScheduledAnalysisSummary(7, 2));
        A.CallTo(() => _analysisQueue.EnqueueAsync(A<AnalysisJob>._, A<CancellationToken>._)).MustHaveHappened(2, Times.Exactly);
    }

    [Fact]
    public async Task RunOnceAsync_WhenMoreRepositoriesThanCap_EnqueuesOnlyCap()
    {
        // Arrange
        GrantLease();
        var systemUnderTests = CreateSystemUnderTests(CreateRepositories(5), enabled: true, schedulerEnabled: true, maxRepositoriesPerRun: 3);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Enqueued.Should().Be(3);
        A.CallTo(() => _analysisQueue.EnqueueAsync(A<AnalysisJob>._, A<CancellationToken>._)).MustHaveHappened(3, Times.Exactly);
    }

    [Fact]
    public async Task RunOnceAsync_WhenRepositoryIsFresh_DoesNotEnqueue()
    {
        // Arrange
        GrantLease();
        var repositories = CreateRepositories(2);
        repositories[0].AnalyzedAt = Now;
        var systemUnderTests = CreateSystemUnderTests(repositories, enabled: true, schedulerEnabled: true, maxRepositoriesPerRun: 10);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Enqueued.Should().Be(1);
    }

    [Fact]
    public async Task RunOnceAsync_WhenLeaseNotAcquired_DoesNothing()
    {
        // Arrange
        A.CallTo(() => _schedulerLease.AcquireAsync(A<CancellationToken>._)).Returns(Task.FromResult<IAsyncDisposable?>(null));
        var systemUnderTests = CreateSystemUnderTests(CreateRepositories(3), enabled: true, schedulerEnabled: true, maxRepositoriesPerRun: 10);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Should().Be(new ScheduledAnalysisSummary(0, 0));
        A.CallTo(() => _refreshRepositoriesUseCase.RefreshAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task RunOnceAsync_WhenDisabled_DoesNothing()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(CreateRepositories(3), enabled: false, schedulerEnabled: true, maxRepositoriesPerRun: 10);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Should().Be(new ScheduledAnalysisSummary(0, 0));
        A.CallTo(() => _refreshRepositoriesUseCase.RefreshAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task RunOnceAsync_WhenSchedulerDisabled_DoesNothing()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(CreateRepositories(3), enabled: true, schedulerEnabled: false, maxRepositoriesPerRun: 10);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Should().Be(new ScheduledAnalysisSummary(0, 0));
        A.CallTo(() => _schedulerLease.AcquireAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task RunOnceAsync_WhenMaxRepositoriesPerRunIsZero_RefreshesButDoesNotEnqueue()
    {
        // Arrange
        GrantLease();
        var systemUnderTests = CreateSystemUnderTests(CreateRepositories(3), enabled: true, schedulerEnabled: true, maxRepositoriesPerRun: 0);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Enqueued.Should().Be(0);
        A.CallTo(() => _refreshRepositoriesUseCase.RefreshAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _analysisQueue.EnqueueAsync(A<AnalysisJob>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private void GrantLease() =>
        A.CallTo(() => _schedulerLease.AcquireAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IAsyncDisposable?>(A.Dummy<IAsyncDisposable>()));

    private ScheduledAnalysisRunner CreateSystemUnderTests(IReadOnlyList<Repository> repositories, bool enabled, bool schedulerEnabled, int maxRepositoriesPerRun)
    {
        A.CallTo(() => _schedulingOptions.Value).Returns(new SchedulingOptions
        {
            Enabled = enabled,
            RepositoriesRefreshMinutes = 10,
            AnalysisIntervalMinutes = 60,
            MaxRepositoriesPerRun = maxRepositoriesPerRun
        });

        var scalingOptions = A.Fake<IOptions<ScalingOptions>>();
        A.CallTo(() => scalingOptions.Value).Returns(new ScalingOptions
        {
            SchedulerEnabled = schedulerEnabled,
            WorkerEnabled = true,
            WorkerConcurrency = 1,
            WorkerMaxAttempts = 1,
            WorkerRetryDelayMilliseconds = 0,
            QueueCapacity = 10
        });

        var dbContext = A.Fake<IRepoHealthCheckerDbContext>();
        A.CallTo(() => dbContext.Repositories).Returns(CreateRepositoryDbSet(repositories));

        return new ScheduledAnalysisRunner(_refreshRepositoriesUseCase, _analysisQueue, _schedulerLease, dbContext, _schedulingOptions, scalingOptions, _timeProvider, _logger);
    }

    private static List<Repository> CreateRepositories(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new Repository { Id = Guid.NewGuid(), SourceCraftId = $"repo-{index}" })
            .ToList();

    private static DbSet<Repository> CreateRepositoryDbSet(IReadOnlyList<Repository> repositories)
    {
        var queryable = (IQueryable<Repository>)new TestAsyncEnumerable<Repository>(repositories);
        var dbSet = A.Fake<DbSet<Repository>>(options => options.Implements(typeof(IQueryable<Repository>)));
        A.CallTo(() => ((IQueryable<Repository>)dbSet).Provider).Returns(queryable.Provider);
        A.CallTo(() => ((IQueryable<Repository>)dbSet).Expression).Returns(queryable.Expression);
        A.CallTo(() => ((IQueryable<Repository>)dbSet).ElementType).Returns(queryable.ElementType);
        A.CallTo(() => ((IQueryable<Repository>)dbSet).GetEnumerator()).ReturnsLazily(() => queryable.GetEnumerator());

        return dbSet;
    }
}
