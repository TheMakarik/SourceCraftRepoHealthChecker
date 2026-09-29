using FakeItEasy;
using FluentAssertions;
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
using SourceCraftRepoHealthChecker.infrastructure.Scheduling;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;
using SourceCraftRepoHealthChecker.UnitTests.Scheduling.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.Scheduling;

public sealed class AnalysisWorkerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IAnalyzeRepositoryUseCase _analyzeRepositoryUseCase = A.Fake<IAnalyzeRepositoryUseCase>();
    private readonly IRepoHealthCheckerDbContext _dbContext = A.Fake<IRepoHealthCheckerDbContext>();
    private readonly ILogger<AnalysisWorker> _logger = A.Dummy<ILogger<AnalysisWorker>>();
    private readonly TimeProvider _timeProvider = new StubTimeProvider(Now);

    [Fact]
    public async Task ProcessAsync_WhenAnalysisSucceeds_ReturnsTrue()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxAttempts: 3);

        // Act
        var actual = await systemUnderTests.ProcessAsync(new AnalysisJob("repo-1"), CancellationToken.None);

        // Assert
        actual.Should().BeTrue();
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ProcessAsync_WhenFirstAttemptFails_RetriesAndSucceeds()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxAttempts: 3);
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("boom")).Once()
            .Then.Returns(Task.FromResult(A.Dummy<AnalyzeRepositoryResult>()));

        // Act
        var actual = await systemUnderTests.ProcessAsync(new AnalysisJob("repo-1"), CancellationToken.None);

        // Assert
        actual.Should().BeTrue();
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._)).MustHaveHappened(2, Times.Exactly);
    }

    [Fact]
    public async Task ProcessAsync_WhenAlwaysFails_ReturnsFalseAfterMaxAttempts()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxAttempts: 2);
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var actual = await systemUnderTests.ProcessAsync(new AnalysisJob("repo-1"), CancellationToken.None);

        // Assert
        actual.Should().BeFalse();
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._)).MustHaveHappened(2, Times.Exactly);
    }

    [Fact]
    public async Task ProcessAsync_WhenAttemptsExhausted_PersistsFailedRun()
    {
        // Arrange
        var repository = new Repository { Id = Guid.NewGuid(), SourceCraftId = "repo-1" };
        var systemUnderTests = CreateSystemUnderTests(maxAttempts: 1, repositories: [repository]);
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var actual = await systemUnderTests.ProcessAsync(new AnalysisJob("repo-1"), CancellationToken.None);

        // Assert
        actual.Should().BeFalse();
        A.CallTo(() => _dbContext.AnalysisRuns.Add(A<AnalysisRun>.That.Matches(run =>
            run.RepositoryId == repository.Id
            && run.Status == AnalysisStatus.Failed
            && run.DataStatus == DataStatus.NoData
            && run.ErrorMessage == "boom"))).MustHaveHappenedOnceExactly();
        A.CallTo(() => _dbContext.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task ProcessAsync_WhenLatestRunAlreadyFailed_DoesNotPersistAnotherRun()
    {
        // Arrange
        var repository = new Repository { Id = Guid.NewGuid(), SourceCraftId = "repo-1" };
        var failedRun = new AnalysisRun { Id = Guid.NewGuid(), RepositoryId = repository.Id, Status = AnalysisStatus.Failed, StartedAt = Now };
        var systemUnderTests = CreateSystemUnderTests(maxAttempts: 1, repositories: [repository], runs: [failedRun]);
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        await systemUnderTests.ProcessAsync(new AnalysisJob("repo-1"), CancellationToken.None);

        // Assert
        A.CallTo(() => _dbContext.AnalysisRuns.Add(A<AnalysisRun>._)).MustNotHaveHappened();
    }

    private AnalysisWorker CreateSystemUnderTests(
        int maxAttempts,
        IReadOnlyList<Repository>? repositories = null,
        IReadOnlyList<AnalysisRun>? runs = null)
    {
        var options = A.Fake<IOptions<ScalingOptions>>();
        A.CallTo(() => options.Value).Returns(new ScalingOptions
        {
            SchedulerEnabled = true,
            WorkerEnabled = true,
            WorkerConcurrency = 1,
            WorkerMaxAttempts = maxAttempts,
            WorkerRetryDelayMilliseconds = 0,
            QueueCapacity = 10
        });

        var systemCallScope = A.Fake<ISourceCraftSystemCallScope>();
        A.CallTo(() => systemCallScope.Begin()).Returns(A.Dummy<IDisposable>());

        A.CallTo(() => _dbContext.Repositories).Returns(CreateDbSet(repositories ?? []));
        A.CallTo(() => _dbContext.AnalysisRuns).Returns(CreateDbSet(runs ?? []));
        A.CallTo(() => _dbContext.SaveChangesAsync(A<CancellationToken>._)).Returns(Task.FromResult(0));

        return new AnalysisWorker(_analyzeRepositoryUseCase, _dbContext, systemCallScope, options, _timeProvider, _logger);
    }

    private static DbSet<T> CreateDbSet<T>(IReadOnlyList<T> items) where T : class
    {
        var queryable = (IQueryable<T>)new TestAsyncEnumerable<T>(items);
        var dbSet = A.Fake<DbSet<T>>(options => options.Implements(typeof(IQueryable<T>)));
        A.CallTo(() => ((IQueryable<T>)dbSet).Provider).Returns(queryable.Provider);
        A.CallTo(() => ((IQueryable<T>)dbSet).Expression).Returns(queryable.Expression);
        A.CallTo(() => ((IQueryable<T>)dbSet).ElementType).Returns(queryable.ElementType);
        A.CallTo(() => ((IQueryable<T>)dbSet).GetEnumerator()).ReturnsLazily(() => queryable.GetEnumerator());

        return dbSet;
    }
}
