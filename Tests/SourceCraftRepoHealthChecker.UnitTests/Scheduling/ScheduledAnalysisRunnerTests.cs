using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
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
    private readonly IRefreshRepositoriesUseCase _refreshRepositoriesUseCase = A.Fake<IRefreshRepositoriesUseCase>();
    private readonly IAnalyzeRepositoryUseCase _analyzeRepositoryUseCase = A.Fake<IAnalyzeRepositoryUseCase>();
    private readonly IOptions<SchedulingOptions> _schedulingOptions = A.Fake<IOptions<SchedulingOptions>>();
    private readonly ILogger<ScheduledAnalysisRunner> _logger = A.Dummy<ILogger<ScheduledAnalysisRunner>>();
    private readonly TimeProvider _timeProvider = new StubTimeProvider(DateTimeOffset.UtcNow);

    [Fact]
    public async Task RunOnceAsync_WhenEnabled_RefreshesCatalogAndReturnsRefreshedCount()
    {
        // Arrange
        var repositories = CreateRepositories(2);
        var systemUnderTests = CreateSystemUnderTests(repositories, enabled: true, maxRepositoriesPerRun: 5);
        A.CallTo(() => _refreshRepositoriesUseCase.RefreshAsync(A<CancellationToken>._)).Returns(7);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Refreshed.Should().Be(7);
        A.CallTo(() => _refreshRepositoriesUseCase.RefreshAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RunOnceAsync_WhenMoreRepositoriesThanCap_AnalyzesOnlyCap()
    {
        // Arrange
        var repositories = CreateRepositories(5);
        var systemUnderTests = CreateSystemUnderTests(repositories, enabled: true, maxRepositoriesPerRun: 3);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Analyzed.Should().Be(3);
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._)).MustHaveHappened(3, Times.Exactly);
    }

    [Fact]
    public async Task RunOnceAsync_WhenRepositoryAnalysisFails_IncrementsFailedAndContinues()
    {
        // Arrange
        var repositories = CreateRepositories(3);
        var failingRepositoryId = repositories[1].SourceCraftId;
        var systemUnderTests = CreateSystemUnderTests(repositories, enabled: true, maxRepositoriesPerRun: 10);
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>.That.Matches(request => request.RepositoryId == failingRepositoryId), A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("analysis failed"));

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Analyzed.Should().Be(2);
        actual.Failed.Should().Be(1);
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._)).MustHaveHappened(3, Times.Exactly);
    }

    [Fact]
    public async Task RunOnceAsync_WhenDisabled_DoesNotRefreshOrAnalyze()
    {
        // Arrange
        var repositories = CreateRepositories(3);
        var systemUnderTests = CreateSystemUnderTests(repositories, enabled: false, maxRepositoriesPerRun: 10);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Should().Be(new ScheduledAnalysisSummary(0, 0, 0));
        A.CallTo(() => _refreshRepositoriesUseCase.RefreshAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task RunOnceAsync_WhenMaxRepositoriesPerRunIsZero_RefreshesButDoesNotAnalyze()
    {
        // Arrange
        var repositories = CreateRepositories(3);
        var systemUnderTests = CreateSystemUnderTests(repositories, enabled: true, maxRepositoriesPerRun: 0);

        // Act
        var actual = await systemUnderTests.RunOnceAsync(CancellationToken.None);

        // Assert
        actual.Analyzed.Should().Be(0);
        actual.Failed.Should().Be(0);
        A.CallTo(() => _refreshRepositoriesUseCase.RefreshAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private ScheduledAnalysisRunner CreateSystemUnderTests(IReadOnlyList<Repository> repositories, bool enabled, int maxRepositoriesPerRun)
    {
        A.CallTo(() => _schedulingOptions.Value).Returns(new SchedulingOptions
        {
            Enabled = enabled,
            RepositoriesRefreshMinutes = 10,
            AnalysisIntervalMinutes = 60,
            MaxRepositoriesPerRun = maxRepositoriesPerRun
        });

        var dbContext = A.Fake<IRepoHealthCheckerDbContext>();
        A.CallTo(() => dbContext.Repositories).Returns(CreateRepositoryDbSet(repositories));

        return new ScheduledAnalysisRunner(_refreshRepositoriesUseCase, _analyzeRepositoryUseCase, dbContext, _schedulingOptions, _timeProvider, _logger);
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
