using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Services;

public sealed class CodeHealthCategoryScoreCalculatorTests
{
    private readonly CategoryScoreCalculator systemUnderTests;

    public CodeHealthCategoryScoreCalculatorTests()
    {
        var options = HealthCheckTestData.CreateOptionsWrapper();
        var normalizer = new MetricNormalizer(options);
        var timeProvider = new StubTimeProvider(HealthCheckTestData.Now);

        systemUnderTests = new CategoryScoreCalculator(options, normalizer, timeProvider);
    }

    [Fact]
    public void Calculate_WhenCodeHealthUnavailable_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(codeHealthAvailability: DataStatus.Unavailable);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
        actual.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_WhenCodeHealthNoData_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(codeHealthAvailability: DataStatus.NoData);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
    }

    [Fact]
    public void Calculate_WhenAvailabilityAvailableButReportNull_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(codeHealth: null);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
    }

    [Fact]
    public void Calculate_WhenNoTodoFixmeOrStaleComments_ReturnsMaximumScore()
    {
        // Arrange
        var report = new CodeHealthReport(0, 0, 0, null);
        var facts = RepositoryFactsBuilder.Build(codeHealth: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.Score.Should().Be(100);
        actual.DataStatus.Should().Be(DataStatus.Available);
        actual.Metrics.Should().HaveCount(3);
    }

    [Fact]
    public void Calculate_WhenTodoPresent_SubtractsTodoPenalty()
    {
        // Arrange
        var report = new CodeHealthReport(5, 0, 0, null);
        var facts = RepositoryFactsBuilder.Build(codeHealth: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.Score.Should().Be(99);
        actual.Metrics.Single(x => x.Code == MetricCode.CodeHealthTodo).NormalizedScore.Should().Be(95);
    }

    [Fact]
    public void Calculate_WhenFixmePresent_SubtractsFixmePenalty()
    {
        // Arrange
        var report = new CodeHealthReport(0, 3, 0, null);
        var facts = RepositoryFactsBuilder.Build(codeHealth: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.Score.Should().Be(99);
        actual.Metrics.Single(x => x.Code == MetricCode.CodeHealthFixme).NormalizedScore.Should().Be(94);
    }

    [Fact]
    public void Calculate_WhenOldestCommentExactlyStaleAge_IsNotStale()
    {
        // Arrange
        var report = new CodeHealthReport(0, 0, 0, TimeSpan.FromDays(60));
        var facts = RepositoryFactsBuilder.Build(codeHealth: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.Score.Should().Be(100);
        var staleComments = actual.Metrics.Single(x => x.Code == MetricCode.CodeHealthStaleComments);
        staleComments.RawValue.Should().Be(60);
        staleComments.NormalizedScore.Should().Be(100);
    }

    [Fact]
    public void Calculate_WhenOldestCommentBeyondStaleAge_AppliesStalePenalty()
    {
        // Arrange
        var report = new CodeHealthReport(0, 0, 0, TimeSpan.FromDays(61));
        var facts = RepositoryFactsBuilder.Build(codeHealth: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.Score.Should().Be(97);
        actual.Metrics.Single(x => x.Code == MetricCode.CodeHealthStaleComments).NormalizedScore.Should().Be(95);
    }

    [Fact]
    public void Calculate_WhenPenaltyExceedsMaximum_ClampsBothMetricsToMinimum()
    {
        // Arrange
        var report = new CodeHealthReport(100, 50, 0, null);
        var facts = RepositoryFactsBuilder.Build(codeHealth: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.Score.Should().Be(63);
        actual.Metrics.Single(x => x.Code == MetricCode.CodeHealthTodo).NormalizedScore.Should().Be(0);
        actual.Metrics.Single(x => x.Code == MetricCode.CodeHealthFixme).NormalizedScore.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenStaleAndRiskyCommentsPresent_CombinesPenalties()
    {
        // Arrange
        var report = new CodeHealthReport(2, 1, 0, TimeSpan.FromDays(61));
        var facts = RepositoryFactsBuilder.Build(codeHealth: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.Score.Should().Be(96);
    }

    [Fact]
    public void Calculate_WhenOldestCommentMissing_ReportsZeroStaleDays()
    {
        // Arrange
        var report = new CodeHealthReport(0, 0, 0, null);
        var facts = RepositoryFactsBuilder.Build(codeHealth: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.CodeHealthStaleComments).RawValue.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenTodoFixmeAndStalePresent_ScoreEqualsWeightedAverageOfMetrics()
    {
        // Arrange
        var report = new CodeHealthReport(4, 3, 0, TimeSpan.FromDays(61));
        var facts = RepositoryFactsBuilder.Build(codeHealth: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CodeHealth, facts);

        // Assert
        var weighted = WeightedAverage(actual.Metrics);
        actual.Score.Should().Be((int)Math.Round(weighted, MidpointRounding.AwayFromZero));
    }

    private static double WeightedAverage(IReadOnlyCollection<MetricScore> metrics)
    {
        var weighted = metrics.Where(x => x.Weight > 0).ToArray();

        return weighted.Sum(x => x.NormalizedScore * x.Weight) / weighted.Sum(x => x.Weight);
    }
}
