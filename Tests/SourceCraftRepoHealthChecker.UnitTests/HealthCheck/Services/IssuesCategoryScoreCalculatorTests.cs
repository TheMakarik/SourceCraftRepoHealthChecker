using AutoFixture;
using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Services;

public sealed class IssuesCategoryScoreCalculatorTests
{
    private readonly Fixture _fixture = new();
    private readonly CategoryScoreCalculator systemUnderTests;

    public IssuesCategoryScoreCalculatorTests()
    {
        var options = HealthCheckTestData.CreateOptionsWrapper();
        var normalizer = new MetricNormalizer(options);
        var timeProvider = new StubTimeProvider(HealthCheckTestData.Now);

        systemUnderTests = new CategoryScoreCalculator(options, normalizer, timeProvider);
    }

    [Fact]
    public void Calculate_WhenCollaborationUnavailable_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(collaborationAvailability: DataStatus.Unavailable);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
        actual.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_WhenCollaborationNoData_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(collaborationAvailability: DataStatus.NoData);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
    }

    [Fact]
    public void Calculate_WhenNoIssues_ReturnsMaximumScoreWithNoDataResponseMetrics()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(issues: []);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.Score.Should().Be(100);
        actual.DataStatus.Should().Be(DataStatus.Available);
        actual.Metrics.Should().HaveCount(5);
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesFirstResponse).DataStatus.Should().Be(DataStatus.NoData);
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesCloseTime).DataStatus.Should().Be(DataStatus.NoData);
    }

    [Fact]
    public void Calculate_WhenSingleOpenIssue_ComputesOpenScore()
    {
        // Arrange
        var issues = new IssueInfo[]
        {
            Issue(IssueState.Open, HealthCheckTestData.Now)
        };
        var facts = RepositoryFactsBuilder.Build(issues: issues);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.Score.Should().Be(63);
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesOpen).RawValue.Should().Be(1);
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesFirstResponse).DataStatus.Should().Be(DataStatus.NoData);
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesCloseTime).DataStatus.Should().Be(DataStatus.NoData);
    }

    [Fact]
    public void Calculate_WhenOnlyClosedIssues_ReportsCloseTime()
    {
        // Arrange
        var issues = new IssueInfo[]
        {
            Issue(IssueState.Closed, HealthCheckTestData.Now.AddDays(-10), HealthCheckTestData.Now)
        };
        var facts = RepositoryFactsBuilder.Build(issues: issues);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.Score.Should().Be(82);
        var closeTime = actual.Metrics.Single(x => x.Code == MetricCode.IssuesCloseTime);
        closeTime.DataStatus.Should().Be(DataStatus.Available);
        closeTime.RawValue.Should().Be(10);
    }

    [Fact]
    public void Calculate_WhenIssueAgeExactlyStaleBoundary_IsNotStale()
    {
        // Arrange
        var issues = new IssueInfo[]
        {
            Issue(IssueState.Open, HealthCheckTestData.Now.AddDays(-30))
        };
        var facts = RepositoryFactsBuilder.Build(issues: issues);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesStale).RawValue.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenIssueAgeBeyondStaleBoundary_IsStale()
    {
        // Arrange
        var issues = new IssueInfo[]
        {
            Issue(IssueState.Open, HealthCheckTestData.Now.AddDays(-31))
        };
        var facts = RepositoryFactsBuilder.Build(issues: issues);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesStale).RawValue.Should().Be(1);
    }

    [Fact]
    public void Calculate_WhenAllOpenIssuesStale_StaleScoreIsMinimum()
    {
        // Arrange
        var issues = new IssueInfo[]
        {
            Issue(IssueState.Open, HealthCheckTestData.Now.AddDays(-40)),
            Issue(IssueState.Open, HealthCheckTestData.Now.AddDays(-50))
        };
        var facts = RepositoryFactsBuilder.Build(issues: issues);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesStale).NormalizedScore.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenFirstResponsePresent_ReportsFirstResponse()
    {
        // Arrange
        var issues = new IssueInfo[]
        {
            Issue(IssueState.Open, HealthCheckTestData.Now.AddDays(-10), firstResponseAt: HealthCheckTestData.Now.AddDays(-7))
        };
        var facts = RepositoryFactsBuilder.Build(issues: issues);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        var firstResponse = actual.Metrics.Single(x => x.Code == MetricCode.IssuesFirstResponse);
        firstResponse.DataStatus.Should().Be(DataStatus.Available);
        firstResponse.RawValue.Should().Be(3);
    }

    [Fact]
    public void Calculate_WhenOpenIssuesExceedMaximum_OpenScoreIsMinimum()
    {
        // Arrange
        var issues = Enumerable.Range(0, 11)
            .Select(_ => Issue(IssueState.Open, HealthCheckTestData.Now))
            .ToArray();
        var facts = RepositoryFactsBuilder.Build(issues: issues);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesOpen).NormalizedScore.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenNoFirstResponseAndNoCloseData_ExcludesMetricsFromScore()
    {
        // Arrange
        var issues = new IssueInfo[]
        {
            Issue(IssueState.Open, HealthCheckTestData.Now.AddDays(-10))
        };
        var facts = RepositoryFactsBuilder.Build(issues: issues);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.Metrics.Where(x => x.DataStatus == DataStatus.NoData).Should().HaveCount(2);
        actual.Metrics.Where(x => x.DataStatus == DataStatus.Available).Should().HaveCount(3);
    }

    [Fact]
    public void Calculate_WhenOpenIssueHasRecentCreatedAtButOldUpdatedAt_IsStale()
    {
        // Arrange
        var issues = new IssueInfo[]
        {
            Issue(IssueState.Open, HealthCheckTestData.Now.AddDays(-1), updatedAt: HealthCheckTestData.Now.AddDays(-40))
        };
        var facts = RepositoryFactsBuilder.Build(issues: issues);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesStale).RawValue.Should().Be(1);
    }

    [Fact]
    public void Calculate_WhenOpenIssueHasFreshUpdatedAt_IsNotStale()
    {
        // Arrange
        var issues = new IssueInfo[]
        {
            Issue(IssueState.Open, HealthCheckTestData.Now.AddDays(-40), updatedAt: HealthCheckTestData.Now.AddDays(-1))
        };
        var facts = RepositoryFactsBuilder.Build(issues: issues);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Issues, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.IssuesStale).RawValue.Should().Be(0);
    }

    private IssueInfo Issue(
        IssueState state,
        DateTimeOffset createdAt,
        DateTimeOffset? closedAt = null,
        DateTimeOffset? firstResponseAt = null,
        DateTimeOffset? updatedAt = null) => new(
        _fixture.Create<string>(),
        _fixture.Create<string>(),
        state,
        _fixture.Create<string>(),
        createdAt,
        closedAt,
        firstResponseAt,
        updatedAt);
}
