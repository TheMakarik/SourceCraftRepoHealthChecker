using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

namespace SourceCraftRepoHealthChecker.IntegrationTests;

public sealed class HealthCheckStatisticsTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly TempFileSystem _tempFileSystem = new();
    private readonly HealthCheckOptions _healthCheckOptions = HealthCheckOptionsLoader.Load();

    public void Dispose() => _tempFileSystem.Dispose();

    [Fact]
    public async Task CheckAsync_WhenHealthyRepository_ReturnsExpectedStatistics()
    {
        // Arrange
        var repositoryPath = CreateRepository("create-healthy-repository.ps1");

        // Act
        var actual = await RunAsync(repositoryPath);

        // Assert
        actual.Score.Should().Be(87);
        actual.MethodologyVersion.Should().Be("1.0");
        actual.CalculatedAt.Should().Be(FixedNow);

        var activity = Category(actual, ScoreCategory.Activity);
        activity.DataStatus.Should().Be(DataStatus.Available);
        activity.Score.Should().Be(57);
        activity.Weight.Should().BeApproximately(0.15, 0.0001);
        activity.Metrics.Should().HaveCount(4);
        Metric(activity, MetricCode.ActivityLastActivity).RawValue.Should().Be(7.08);
        Metric(activity, MetricCode.ActivityLastActivity).NormalizedScore.Should().Be(100);
        Metric(activity, MetricCode.ActivityCommitFrequency).RawValue.Should().Be(5);
        Metric(activity, MetricCode.ActivityCommitFrequency).NormalizedScore.Should().BeApproximately(100.0 * 5 / 30, 0.0001);
        Metric(activity, MetricCode.ActivityContributors).RawValue.Should().Be(3);
        Metric(activity, MetricCode.ActivityContributors).NormalizedScore.Should().Be(60);
        Metric(activity, MetricCode.ActivityReleases).RawValue.Should().Be(2);
        Metric(activity, MetricCode.ActivityReleases).NormalizedScore.Should().Be(50);

        var documentation = Category(actual, ScoreCategory.Documentation);
        documentation.DataStatus.Should().Be(DataStatus.Available);
        documentation.Score.Should().Be(100);
        documentation.Metrics.Should().HaveCount(6);
        documentation.Metrics.Should().OnlyContain(metric => metric.RawValue == 1 && metric.NormalizedScore == 100);

        var codeHealth = Category(actual, ScoreCategory.CodeHealth);
        codeHealth.DataStatus.Should().Be(DataStatus.Available);
        codeHealth.Score.Should().Be(100);
        codeHealth.Metrics.Should().HaveCount(3);
        Metric(codeHealth, MetricCode.CodeHealthTodo).RawValue.Should().Be(0);
        Metric(codeHealth, MetricCode.CodeHealthFixme).RawValue.Should().Be(0);
        Metric(codeHealth, MetricCode.CodeHealthStaleComments).RawValue.Should().Be(0);

        AssertApiCategoriesHaveNoData(actual);
        actual.Recommendations.Should().HaveCount(2);
        actual.Recommendations.Should().ContainSingle(recommendation => recommendation.Priority == RecommendationPriority.Medium)
            .Which.Problem.Should().Contain("Активность");
        actual.Recommendations.Should().ContainSingle(recommendation => recommendation.Priority == RecommendationPriority.Low)
            .Which.Problem.Should().Contain("AppSec SourceCraft");
    }

    [Fact]
    public async Task CheckAsync_WhenProblematicRepository_ReturnsExpectedStatistics()
    {
        // Arrange
        var repositoryPath = CreateRepository("create-problematic-repository.ps1");

        // Act
        var actual = await RunAsync(repositoryPath);

        // Assert
        actual.Score.Should().Be(40);
        actual.CalculatedAt.Should().Be(FixedNow);

        var activity = Category(actual, ScoreCategory.Activity);
        activity.DataStatus.Should().Be(DataStatus.Available);
        activity.Score.Should().Be(5);
        activity.Metrics.Should().HaveCount(4);
        Metric(activity, MetricCode.ActivityLastActivity).RawValue.Should().Be(759.08);
        Metric(activity, MetricCode.ActivityLastActivity).NormalizedScore.Should().Be(0);
        Metric(activity, MetricCode.ActivityCommitFrequency).RawValue.Should().Be(0);
        Metric(activity, MetricCode.ActivityCommitFrequency).NormalizedScore.Should().Be(0);
        Metric(activity, MetricCode.ActivityContributors).RawValue.Should().Be(1);
        Metric(activity, MetricCode.ActivityContributors).NormalizedScore.Should().Be(20);
        Metric(activity, MetricCode.ActivityReleases).RawValue.Should().Be(0);
        Metric(activity, MetricCode.ActivityReleases).NormalizedScore.Should().Be(0);

        var documentation = Category(actual, ScoreCategory.Documentation);
        documentation.DataStatus.Should().Be(DataStatus.Available);
        documentation.Score.Should().Be(0);
        documentation.Metrics.Should().HaveCount(6);
        documentation.Metrics.Should().OnlyContain(metric => metric.RawValue == 0 && metric.NormalizedScore == 0);

        var codeHealth = Category(actual, ScoreCategory.CodeHealth);
        codeHealth.DataStatus.Should().Be(DataStatus.Available);
        codeHealth.Score.Should().Be(95);
        codeHealth.Metrics.Should().HaveCount(3);
        Metric(codeHealth, MetricCode.CodeHealthTodo).RawValue.Should().Be(4);
        Metric(codeHealth, MetricCode.CodeHealthTodo).NormalizedScore.Should().Be(96);
        Metric(codeHealth, MetricCode.CodeHealthFixme).RawValue.Should().Be(3);
        Metric(codeHealth, MetricCode.CodeHealthFixme).NormalizedScore.Should().Be(94);
        Metric(codeHealth, MetricCode.CodeHealthStaleComments).RawValue.Should().BeGreaterThan(180);
        Metric(codeHealth, MetricCode.CodeHealthStaleComments).NormalizedScore.Should().Be(95);

        AssertApiCategoriesHaveNoData(actual);
        actual.Recommendations.Should().HaveCount(3);
        actual.Recommendations.Should().ContainSingle(recommendation => recommendation.Priority == RecommendationPriority.Low)
            .Which.Problem.Should().Contain("AppSec SourceCraft");
        actual.Recommendations.Count(recommendation => recommendation.Priority == RecommendationPriority.Critical).Should().Be(2);
        actual.Recommendations.Select(recommendation => recommendation.Problem)
            .Should().Contain(problem => problem.Contains("Активность"))
            .And.Contain(problem => problem.Contains("Отсутствует"));
    }

    [Fact]
    public async Task CheckAsync_WhenUndocumentedRepository_ReturnsExpectedStatistics()
    {
        // Arrange
        var repositoryPath = CreateRepository("create-undocumented-repository.ps1");

        // Act
        var actual = await RunAsync(repositoryPath);

        // Assert
        actual.Score.Should().Be(59);
        actual.CalculatedAt.Should().Be(FixedNow);

        var activity = Category(actual, ScoreCategory.Activity);
        activity.DataStatus.Should().Be(DataStatus.Available);
        activity.Score.Should().Be(62);
        activity.Metrics.Should().HaveCount(4);
        Metric(activity, MetricCode.ActivityLastActivity).RawValue.Should().Be(3.08);
        Metric(activity, MetricCode.ActivityLastActivity).NormalizedScore.Should().Be(100);
        Metric(activity, MetricCode.ActivityCommitFrequency).RawValue.Should().Be(5);
        Metric(activity, MetricCode.ActivityCommitFrequency).NormalizedScore.Should().BeApproximately(100.0 * 5 / 30, 0.0001);
        Metric(activity, MetricCode.ActivityContributors).RawValue.Should().Be(4);
        Metric(activity, MetricCode.ActivityContributors).NormalizedScore.Should().Be(80);
        Metric(activity, MetricCode.ActivityReleases).RawValue.Should().Be(2);
        Metric(activity, MetricCode.ActivityReleases).NormalizedScore.Should().Be(50);

        var documentation = Category(actual, ScoreCategory.Documentation);
        documentation.DataStatus.Should().Be(DataStatus.Available);
        documentation.Score.Should().Be(0);
        documentation.Metrics.Should().HaveCount(6);
        documentation.Metrics.Should().OnlyContain(metric => metric.RawValue == 0 && metric.NormalizedScore == 0);

        var codeHealth = Category(actual, ScoreCategory.CodeHealth);
        codeHealth.DataStatus.Should().Be(DataStatus.Available);
        codeHealth.Score.Should().Be(100);
        Metric(codeHealth, MetricCode.CodeHealthTodo).RawValue.Should().Be(0);
        Metric(codeHealth, MetricCode.CodeHealthFixme).RawValue.Should().Be(0);

        AssertApiCategoriesHaveNoData(actual);
        actual.Recommendations.Should().HaveCount(3);
        actual.Recommendations.Select(recommendation => recommendation.Priority)
            .Should().BeEquivalentTo([RecommendationPriority.Medium, RecommendationPriority.Critical, RecommendationPriority.Low]);
    }

    private string CreateRepository(string scriptFileName)
    {
        var repositoryPath = Path.Join(_tempFileSystem.Path, Path.GetFileNameWithoutExtension(scriptFileName));
        TestRepositoryFactory.Create(repositoryPath, scriptFileName);

        return repositoryPath;
    }

    private async Task<HealthCheckResult> RunAsync(string repositoryPath)
    {
        var facts = await RepositoryFactsFactory.ReadAsync(repositoryPath, CancellationToken.None);

        var options = Options.Create(_healthCheckOptions);
        var timeProvider = new FixedTimeProvider(FixedNow);
        var normalizer = new MetricNormalizer(options);
        var categoryScoreCalculator = new CategoryScoreCalculator(options, normalizer, timeProvider);
        var healthScoreCalculator = new HealthScoreCalculator(options);
        var recommendationGenerator = new RecommendationGenerator(options);
        var systemUnderTests = new HealthCheckEngine(
            options,
            categoryScoreCalculator,
            healthScoreCalculator,
            recommendationGenerator,
            timeProvider);

        return await systemUnderTests.CheckAsync(facts, CancellationToken.None);
    }

    private static void AssertApiCategoriesHaveNoData(HealthCheckResult result)
    {
        ScoreCategory[] apiCategories = [ScoreCategory.Security, ScoreCategory.CiCd, ScoreCategory.Issues];

        foreach (var apiCategory in apiCategories)
        {
            var category = Category(result, apiCategory);
            category.DataStatus.Should().Be(DataStatus.NoData);
            category.Score.Should().Be(0);
            category.Metrics.Should().BeEmpty();
        }
    }

    private static CategoryScoreResult Category(HealthCheckResult result, ScoreCategory category) =>
        result.Categories.Single(candidate => candidate.Category == category);

    private static MetricScore Metric(CategoryScoreResult category, MetricCode code) =>
        category.Metrics.Single(candidate => candidate.Code == code);
}
