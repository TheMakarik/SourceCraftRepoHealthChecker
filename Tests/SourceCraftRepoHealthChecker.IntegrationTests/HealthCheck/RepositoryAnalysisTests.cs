using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;
using SourceCraftRepoHealthChecker.infrastructure.Reports;

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
        exportReport = new ExportRepositoryReportUseCase(getAnalysis, new QuestPdfReportRenderer());
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
        actual.Metrics.Should().ContainSingle(metric => metric.Code == MetricCode.CodeHealthTodo);
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
    public async Task GetAsync_WhenMarkdownRequested_ReturnsReportWithScoreCategoriesAndRecommendations()
    {
        // Act
        var actual = await exportReport.GetAsync("sc-1", ReportFormat.Markdown, CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual!.ContentType.Should().Be("text/markdown; charset=utf-8");
        var content = Encoding.UTF8.GetString(actual.Content);
        content.Should().Contain("# Repo Health: owner/demo");
        content.Should().Contain("78/100");
        content.Should().Contain("CodeHealth");
        content.Should().Contain("Почему важно");
        content.Should().Contain("Устраните уязвимости");
    }

    [Fact]
    public async Task GetAsync_WhenJsonRequested_ReturnsNonEmptyJsonReport()
    {
        // Act
        var actual = await exportReport.GetAsync("sc-1", ReportFormat.Json, CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual!.ContentType.Should().Be("application/json");
        actual.Content.Should().NotBeEmpty();
        var content = Encoding.UTF8.GetString(actual.Content);
        content.Should().Contain("\"score\":78");
        content.Should().Contain("owner/demo");
    }

    [Fact]
    public async Task GetAsync_WhenHtmlRequested_ReturnsNonEmptySelfContainedHtmlReport()
    {
        // Act
        var actual = await exportReport.GetAsync("sc-1", ReportFormat.Html, CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual!.ContentType.Should().Be("text/html; charset=utf-8");
        var content = Encoding.UTF8.GetString(actual.Content);
        content.Should().StartWith("<!DOCTYPE html>");
        content.Should().Contain("<style>");
        content.Should().Contain("78/100");
    }

    [Fact]
    public async Task GetAsync_WhenPdfRequested_ReturnsNonEmptyPdfReport()
    {
        // Act
        var actual = await exportReport.GetAsync("sc-1", ReportFormat.Pdf, CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual!.ContentType.Should().Be("application/pdf");
        actual.Content.Should().NotBeEmpty();
        Encoding.ASCII.GetString(actual.Content, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task GetAsync_WhenRepositoryUnknown_ExportReturnsNull()
    {
        // Act
        var actual = await exportReport.GetAsync("missing", ReportFormat.Markdown, CancellationToken.None);

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

        run.Metrics.Add(new MetricScore
        {
            Id = Guid.NewGuid(),
            Code = MetricCode.CodeHealthTodo,
            RawValue = 4,
            NormalizedScore = 96,
            Weight = 1,
            DataStatus = DataStatus.Available
        });

        run.Recommendations.Add(new Recommendation
        {
            Id = Guid.NewGuid(),
            Priority = RecommendationPriority.Medium,
            Title = "Документация",
            Problem = "Документация: 40/100",
            WhyImportant = "Документация упрощает сопровождение.",
            Evidence = "Отсутствует: лицензия.",
            Action = "Улучшите документацию",
            ExpectedImpact = "+10 баллов",
            SourceReference = "Documentation"
        });
        run.Recommendations.Add(new Recommendation
        {
            Id = Guid.NewGuid(),
            Priority = RecommendationPriority.Critical,
            Title = "Уязвимости",
            Problem = "Обнаружены критические уязвимости",
            WhyImportant = "Уязвимости влияют на безопасность.",
            Evidence = "Открытые уязвимости AppSec: критических 1.",
            Action = "Устраните уязвимости",
            ExpectedImpact = "+20 баллов",
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
