using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;

namespace SourceCraftRepoHealthChecker.IntegrationTests.HealthCheck;

public sealed class RepositoryAnalysisTests : IDisposable
{
    private static readonly DateTimeOffset CompletedAt = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly RepoHealthCheckerDbContext _context;
    private readonly GetRepositoryAnalysisUseCase getAnalysis;
    private readonly ExportRepositoryReportUseCase exportReport;

    public RepositoryAnalysisTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = TestDbContextFactory.Create(_connection);
        _context.Database.EnsureCreated();
        Seed(_context);
        _context.SaveChanges();

        var options = Options.Create(TestHealthCheckOptionsFactory.Create());
        getAnalysis = new GetRepositoryAnalysisUseCase(_context, options);
        exportReport = new ExportRepositoryReportUseCase(getAnalysis);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetAsync_WhenRepositoryAnalyzed_ReturnsScoreCategoriesHighlightsAndRecommendations()
    {
        // Act
        var actual = await getAnalysis.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual!.Score.Should().Be(78);
        actual.AnalyzedAt.Should().Be(CompletedAt);
        actual.Categories.Should().HaveCount(6);
        actual.Strengths.Select(item => item.Category).Should().Contain(ScoreCategory.Security).And.Contain(ScoreCategory.Documentation);
        actual.Weaknesses.Select(item => item.Category).Should().Contain(ScoreCategory.CodeHealth);
        actual.Recommendations.Should().HaveCount(2);
        actual.Recommendations.First().Priority.Should().Be(RecommendationPriority.Critical);
    }

    [Fact]
    public async Task GetAsync_WhenRepositoryUnknown_ReturnsNull()
    {
        // Act
        var actual = await getAnalysis.GetAsync("missing", CancellationToken.None);

        // Assert
        actual.Should().BeNull();
    }

    [Fact]
    public async Task GetMarkdownAsync_WhenRepositoryAnalyzed_ContainsScoreCategoriesAndRecommendations()
    {
        // Act
        var actual = await exportReport.GetMarkdownAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual.Should().Contain("# Repo Health: owner/demo");
        actual.Should().Contain("78/100");
        actual.Should().Contain("CodeHealth");
        actual.Should().Contain("Устраните уязвимости");
    }

    [Fact]
    public async Task GetMarkdownAsync_WhenRepositoryUnknown_ReturnsNull()
    {
        // Act
        var actual = await exportReport.GetMarkdownAsync("missing", CancellationToken.None);

        // Assert
        actual.Should().BeNull();
    }

    private static void Seed(RepoHealthCheckerDbContext context)
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            SourceCraftId = "sc-1",
            Name = "demo",
            FullName = "owner/demo",
            Url = "https://sourcecraft.dev/owner/demo",
            Language = "C#",
            LikesCount = 5,
            LastActivityAt = CompletedAt,
            CreatedAt = CompletedAt
        };

        var run = new AnalysisRun
        {
            Id = Guid.NewGuid(),
            Score = 78,
            Status = AnalysisStatus.Completed,
            DataStatus = DataStatus.Available,
            StartedAt = CompletedAt,
            CompletedAt = CompletedAt
        };

        AddCategory(run, ScoreCategory.Security, 95);
        AddCategory(run, ScoreCategory.Documentation, 90);
        AddCategory(run, ScoreCategory.CodeHealth, 40);
        AddCategory(run, ScoreCategory.Activity, 70);
        AddCategory(run, ScoreCategory.CiCd, 72);
        AddCategory(run, ScoreCategory.Issues, 80);

        run.Recommendations.Add(new Recommendation
        {
            Id = Guid.NewGuid(),
            Priority = RecommendationPriority.Medium,
            Title = "Документация",
            Problem = "Документация: 40/100",
            Action = "Улучшите документацию",
            ExpectedImpact = "+10",
            SourceReference = "Documentation"
        });
        run.Recommendations.Add(new Recommendation
        {
            Id = Guid.NewGuid(),
            Priority = RecommendationPriority.Critical,
            Title = "Уязвимости",
            Problem = "Обнаружены критические уязвимости",
            Action = "Устраните уязвимости",
            ExpectedImpact = "+20",
            SourceReference = "Security"
        });

        repository.AnalysisRuns.Add(run);
        context.Repositories.Add(repository);
    }

    private static void AddCategory(AnalysisRun run, ScoreCategory category, int score) =>
        run.CategoryScores.Add(new CategoryScore
        {
            Id = Guid.NewGuid(),
            Category = category,
            Score = score,
            DataStatus = DataStatus.Available
        });
}
