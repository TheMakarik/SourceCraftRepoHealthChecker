using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.Scheduling.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.UseCases;

public sealed class GetRepositoryHistoryUseCaseTests
{
    private static readonly DateTimeOffset January = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset February = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset March = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_WhenCompletedRunsExist_ReturnsPointsOldestToNewest()
    {
        // Arrange
        var repositoryId = Guid.NewGuid();
        var dbContext = CreateDbContext(
            [new Repository { Id = repositoryId, SourceCraftId = "sc-1" }],
            [
                CreateRun(repositoryId, 70, February, AnalysisStatus.Completed),
                CreateRun(repositoryId, 80, March, AnalysisStatus.Completed),
                CreateRun(repositoryId, 60, January, AnalysisStatus.Completed),
                CreateRun(repositoryId, 99, null, AnalysisStatus.Running)
            ]);
        var systemUnderTests = CreateSystemUnderTests(dbContext, maxPoints: 100);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Select(point => point.Score).Should().ContainInOrder(60, 70, 80);
    }

    [Fact]
    public async Task GetAsync_WhenRepositoryUnknown_ReturnsEmpty()
    {
        // Arrange
        var dbContext = CreateDbContext([], []);
        var systemUnderTests = CreateSystemUnderTests(dbContext, maxPoints: 100);

        // Act
        var actual = await systemUnderTests.GetAsync("missing", CancellationToken.None);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_WhenMoreRunsThanCap_ReturnsLatestPoints()
    {
        // Arrange
        var repositoryId = Guid.NewGuid();
        var dbContext = CreateDbContext(
            [new Repository { Id = repositoryId, SourceCraftId = "sc-1" }],
            [
                CreateRun(repositoryId, 60, January, AnalysisStatus.Completed),
                CreateRun(repositoryId, 70, February, AnalysisStatus.Completed),
                CreateRun(repositoryId, 80, March, AnalysisStatus.Completed)
            ]);
        var systemUnderTests = CreateSystemUnderTests(dbContext, maxPoints: 2);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Select(point => point.Score).Should().ContainInOrder(70, 80);
    }

    private static GetRepositoryHistoryUseCase CreateSystemUnderTests(IRepoHealthCheckerDbContext dbContext, int maxPoints) =>
        new(dbContext, Options.Create(new RepositoryHistoryOptions { MaxPoints = maxPoints }));

    private static AnalysisRun CreateRun(Guid repositoryId, int score, DateTimeOffset? completedAt, AnalysisStatus status) => new()
    {
        Id = Guid.NewGuid(),
        RepositoryId = repositoryId,
        Score = score,
        Status = status,
        StartedAt = completedAt ?? March,
        CompletedAt = completedAt
    };

    private static IRepoHealthCheckerDbContext CreateDbContext(IReadOnlyList<Repository> repositories, IReadOnlyList<AnalysisRun> runs)
    {
        var dbContext = A.Fake<IRepoHealthCheckerDbContext>();
        A.CallTo(() => dbContext.Repositories).Returns(CreateDbSet(repositories));
        A.CallTo(() => dbContext.AnalysisRuns).Returns(CreateDbSet(runs));
        return dbContext;
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
