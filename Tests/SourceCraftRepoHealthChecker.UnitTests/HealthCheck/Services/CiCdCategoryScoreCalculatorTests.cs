using AutoFixture;
using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Services;

public sealed class CiCdCategoryScoreCalculatorTests
{
    private readonly Fixture _fixture = new();
    private readonly CategoryScoreCalculator systemUnderTests;

    public CiCdCategoryScoreCalculatorTests()
    {
        var options = HealthCheckTestData.CreateOptionsWrapper();
        var normalizer = new MetricNormalizer(options);
        var timeProvider = new StubTimeProvider(HealthCheckTestData.Now);

        systemUnderTests = new CategoryScoreCalculator(options, normalizer, timeProvider);
    }

    [Fact]
    public void Calculate_WhenPipelineUnavailable_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(pipelineAvailability: DataStatus.Unavailable);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CiCd, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
        actual.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_WhenPipelineNoData_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(pipelineAvailability: DataStatus.NoData);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CiCd, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
    }

    [Fact]
    public void Calculate_WhenNoPipelineRuns_ReturnsMinimumScoreWithPresenceMetric()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(pipelineRuns: []);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CiCd, facts);

        // Assert
        actual.Score.Should().Be(0);
        actual.DataStatus.Should().Be(DataStatus.Available);
        actual.Metrics.Should().ContainSingle();
        actual.Metrics.Single().Code.Should().Be(MetricCode.CiCdPresence);
        actual.Metrics.Single().RawValue.Should().Be(0);
        actual.Metrics.Single().NormalizedScore.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenSuccessfulRunsPresent_ComputesPresenceSuccessAndDuration()
    {
        // Arrange
        var runs = new PipelineRun[]
        {
            Run(PipelineStatus.Success, HealthCheckTestData.Now, HealthCheckTestData.Now.AddMinutes(30)),
            Run(PipelineStatus.Success, HealthCheckTestData.Now, HealthCheckTestData.Now.AddMinutes(30))
        };
        var facts = RepositoryFactsBuilder.Build(pipelineRuns: runs);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CiCd, facts);

        // Assert
        actual.Score.Should().Be(57);
        actual.Metrics.Single(x => x.Code == MetricCode.CiCdPresence).RawValue.Should().Be(2);
        actual.Metrics.Single(x => x.Code == MetricCode.CiCdSuccessRatio).RawValue.Should().Be(1);
        actual.Metrics.Single(x => x.Code == MetricCode.CiCdPipelineDuration).RawValue.Should().Be(30);
    }

    [Fact]
    public void Calculate_WhenHalfRunsFailed_ComputesSuccessRatio()
    {
        // Arrange
        var runs = new PipelineRun[]
        {
            Run(PipelineStatus.Success, HealthCheckTestData.Now, HealthCheckTestData.Now),
            Run(PipelineStatus.Failed, HealthCheckTestData.Now, HealthCheckTestData.Now)
        };
        var facts = RepositoryFactsBuilder.Build(pipelineRuns: runs);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CiCd, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.CiCdSuccessRatio).RawValue.Should().Be(0.5);
    }

    [Fact]
    public void Calculate_WhenOnlyFailedFinishedRuns_ReturnsZeroSuccessRatio()
    {
        // Arrange
        var runs = new PipelineRun[]
        {
            Run(PipelineStatus.Failed, HealthCheckTestData.Now, HealthCheckTestData.Now.AddMinutes(5))
        };
        var facts = RepositoryFactsBuilder.Build(pipelineRuns: runs);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CiCd, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.CiCdSuccessRatio).RawValue.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenRunsSkippedOrCanceled_ExcludesThemFromSuccessRatio()
    {
        // Arrange
        var runs = new PipelineRun[]
        {
            Run(PipelineStatus.Skipped, HealthCheckTestData.Now, HealthCheckTestData.Now.AddMinutes(1)),
            Run(PipelineStatus.Canceled, HealthCheckTestData.Now, HealthCheckTestData.Now.AddMinutes(1))
        };
        var facts = RepositoryFactsBuilder.Build(pipelineRuns: runs);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CiCd, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.CiCdSuccessRatio).RawValue.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenNoRunFinished_ScoresDurationAtMaximum()
    {
        // Arrange
        var runs = new PipelineRun[]
        {
            Run(PipelineStatus.Running, HealthCheckTestData.Now, null)
        };
        var facts = RepositoryFactsBuilder.Build(pipelineRuns: runs);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CiCd, facts);

        // Assert
        actual.Score.Should().Be(37);
        var duration = actual.Metrics.Single(x => x.Code == MetricCode.CiCdPipelineDuration);
        duration.RawValue.Should().Be(0);
        duration.NormalizedScore.Should().Be(100);
    }

    [Fact]
    public void Calculate_WhenFinishedRunsHaveDurations_AveragesDurations()
    {
        // Arrange
        var runs = new PipelineRun[]
        {
            Run(PipelineStatus.Success, HealthCheckTestData.Now, HealthCheckTestData.Now.AddMinutes(10)),
            Run(PipelineStatus.Success, HealthCheckTestData.Now, HealthCheckTestData.Now.AddMinutes(30))
        };
        var facts = RepositoryFactsBuilder.Build(pipelineRuns: runs);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CiCd, facts);

        // Assert
        var duration = actual.Metrics.Single(x => x.Code == MetricCode.CiCdPipelineDuration);
        duration.RawValue.Should().Be(20);
        duration.NormalizedScore.Should().BeApproximately(66.67, 0.01);
    }

    [Fact]
    public void Calculate_WhenRunsCountBelowFullScore_NormalizesPresence()
    {
        // Arrange
        var runs = new PipelineRun[]
        {
            Run(PipelineStatus.Success, HealthCheckTestData.Now, HealthCheckTestData.Now)
        };
        var facts = RepositoryFactsBuilder.Build(pipelineRuns: runs);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.CiCd, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.CiCdPresence).NormalizedScore.Should().Be(10);
    }

    private PipelineRun Run(PipelineStatus status, DateTimeOffset startedAt, DateTimeOffset? finishedAt) =>
        new(_fixture.Create<string>(), status, "main", startedAt, finishedAt);
}
