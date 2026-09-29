using FluentAssertions;
using Microsoft.Data.Sqlite;
using SourceCraftRepoHealthChecker.Application.Rating.Models;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Rating;

public sealed class RepositoryLeaderboardTests : IDisposable
{
    private static readonly DateTimeOffset ActivityA = new(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActivityB = new(2026, 1, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActivityC = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly RepoHealthCheckerDbContext _context;
    private readonly GetRepositoryLeaderboardUseCase systemUnderTests;

    public RepositoryLeaderboardTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = TestDbContextFactory.Create(_connection);
        _context.Database.EnsureCreated();
        Seed(_context);
        _context.SaveChanges();
        systemUnderTests = new GetRepositoryLeaderboardUseCase(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetAsync_WhenSortedByScore_OrdersDescendingWithPlace()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Score, 1, 20), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("b", "a", "c");
        actual.Items.Select(item => item.Score).Should().ContainInOrder(90, 80, null);
        actual.Items.Select(item => item.Place).Should().ContainInOrder(1, 2, 3);
        actual.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_WhenRepositoryIsPrivate_ExcludesItFromLeaderboard()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Score, 1, 20), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().NotContain("private");
        actual.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_WhenLanguageFilterDiffersInCase_ReturnsMatchingRepositories()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery(["c#"], RepositoryLeaderboardSort.Score, 1, 20), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("a", "c");
        actual.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_WhenLanguageFilter_ReturnsOnlyMatchingRepositories()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery(["C#"], RepositoryLeaderboardSort.Score, 1, 20), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("a", "c");
        actual.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_WhenMultipleLanguagesFilter_ReturnsUnion()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery(["c#", "go"], RepositoryLeaderboardSort.Score, 1, 20), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("b", "a", "c");
        actual.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_WhenSortedByLikes_OrdersByLikesDescending()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Likes, 1, 20), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("c", "a", "b");
    }

    [Fact]
    public async Task GetAsync_WhenSortedByActivity_OrdersByLastActivityDescending()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Activity, 1, 20), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("b", "a", "c");
    }

    [Fact]
    public async Task GetAsync_WithPagination_ReturnsRequestedPageWithAbsolutePlaces()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Score, 2, 2), CancellationToken.None);

        // Assert
        actual.Items.Should().HaveCount(1);
        actual.Items.Single().SourceCraftId.Should().Be("c");
        actual.Items.Single().Place.Should().Be(3);
        actual.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_WhenHasCiFilterTrue_ReturnsOnlyRepositoriesWithCi()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Score, 1, 20, HasCi: true), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("b");
        actual.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_WhenHasCiFilterFalse_ReturnsOnlyRepositoriesWithoutCi()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Score, 1, 20, HasCi: false), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("a", "c");
        actual.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_WhenScoreRangeSpecified_ReturnsRepositoriesWithinRange()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Score, 1, 20, MinScore: 80, MaxScore: 90), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("b", "a");
        actual.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_WhenMinScoreSpecified_ExcludesRepositoriesWithoutScore()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Score, 1, 20, MinScore: 0), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("b", "a");
        actual.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_WhenScoreRangeReversed_NormalizesBounds()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Score, 1, 20, MinScore: 90, MaxScore: 80), CancellationToken.None);

        // Assert
        actual.Items.Select(item => item.SourceCraftId).Should().ContainInOrder("b", "a");
        actual.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_WhenSortedByScore_ReturnsPlaceDeltaFromPreviousRun()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Score, 1, 20), CancellationToken.None);

        // Assert
        actual.Items.Single(item => item.SourceCraftId == "a").PlaceDelta.Should().Be(-1);
        actual.Items.Single(item => item.SourceCraftId == "b").PlaceDelta.Should().BeNull();
        actual.Items.Single(item => item.SourceCraftId == "c").PlaceDelta.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenSortedByLikes_ReturnsNoPlaceDelta()
    {
        // Act
        var actual = await systemUnderTests.GetAsync(new RepositoryLeaderboardQuery([], RepositoryLeaderboardSort.Likes, 1, 20), CancellationToken.None);

        // Assert
        actual.Items.Should().OnlyContain(item => item.PlaceDelta == null);
    }

    private static void Seed(RepoHealthCheckerDbContext context)
    {
        var repositoryA = CreateRepository("a", "C#", likes: 10, activity: ActivityA);
        repositoryA.AnalysisRuns.Add(CreateRun(80, ActivityA.AddDays(1)));
        repositoryA.AnalysisRuns.Add(CreateRun(10, ActivityA.AddDays(-1)));

        var repositoryB = CreateRepository("b", "Go", likes: 5, activity: ActivityB);
        var runB = CreateRun(90, ActivityB.AddDays(1));
        runB.Metrics.Add(CreateMetric(MetricCode.CiCdPresence, 1));
        repositoryB.AnalysisRuns.Add(runB);

        var repositoryC = CreateRepository("c", "C#", likes: 50, activity: ActivityC);
        repositoryC.AnalysisRuns.Add(CreateRun(100, ActivityC.AddDays(1), DataStatus.NoData));

        var privateRepository = CreateRepository("private", "C#", likes: 100, activity: ActivityB);
        privateRepository.IsPrivate = true;
        privateRepository.AnalysisRuns.Add(CreateRun(99, ActivityB.AddDays(1)));

        context.Repositories.AddRange(repositoryA, repositoryB, repositoryC, privateRepository);
    }

    private static Repository CreateRepository(string sourceCraftId, string language, int likes, DateTimeOffset activity) => new()
    {
        Id = Guid.NewGuid(),
        SourceCraftId = sourceCraftId,
        Name = sourceCraftId,
        FullName = $"owner/{sourceCraftId}",
        Url = $"https://sourcecraft.dev/owner/{sourceCraftId}",
        Language = language,
        LikesCount = likes,
        LastActivityAt = activity,
        CreatedAt = activity
    };

    private static AnalysisRun CreateRun(int score, DateTimeOffset completedAt, DataStatus dataStatus = DataStatus.Available) => new()
    {
        Id = Guid.NewGuid(),
        Score = score,
        Status = AnalysisStatus.Completed,
        DataStatus = dataStatus,
        StartedAt = completedAt,
        CompletedAt = completedAt
    };

    private static MetricScore CreateMetric(MetricCode code, double rawValue) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        RawValue = rawValue,
        NormalizedScore = rawValue > 0 ? 100 : 0,
        Weight = 1,
        DataStatus = DataStatus.Available
    };
}
