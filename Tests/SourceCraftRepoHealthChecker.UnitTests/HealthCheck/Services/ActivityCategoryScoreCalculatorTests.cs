using AutoFixture;
using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Services;

public sealed class ActivityCategoryScoreCalculatorTests
{
    private readonly Fixture _fixture = new();
    private readonly CategoryScoreCalculator systemUnderTests;

    public ActivityCategoryScoreCalculatorTests()
    {
        var options = HealthCheckTestData.CreateOptionsWrapper();
        var normalizer = new MetricNormalizer(options);
        var timeProvider = new StubTimeProvider(HealthCheckTestData.Now);

        systemUnderTests = new CategoryScoreCalculator(options, normalizer, timeProvider);
    }

    [Fact]
    public void Calculate_WhenActivityUnavailable_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(activityAvailability: DataStatus.Unavailable);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
        actual.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_WhenActivityNoData_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(activityAvailability: DataStatus.NoData);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
    }

    [Fact]
    public void Calculate_WhenCollaborationUnavailable_ReportsFourMetrics()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(collaborationAvailability: DataStatus.Unavailable);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.Available);
        actual.Metrics.Should().HaveCount(4);
    }

    [Fact]
    public void Calculate_WhenCollaborationAvailable_AddsMergeRequestMetric()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(collaborationAvailability: DataStatus.Available);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.Metrics.Should().HaveCount(5);
        actual.Metrics.Should().ContainSingle(x => x.Code == MetricCode.ActivityMergeRequests);
    }

    [Fact]
    public void Calculate_WhenLastActivityNow_LastActivityMetricIsMaximum()
    {
        // Arrange
        var repository = HealthCheckTestData.CreateRepository(HealthCheckTestData.Now);
        var facts = RepositoryFactsBuilder.Build(repository: repository);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        var metric = actual.Metrics.Single(x => x.Code == MetricCode.ActivityLastActivity);
        metric.RawValue.Should().Be(0);
        metric.NormalizedScore.Should().Be(100);
    }

    [Fact]
    public void Calculate_WhenLastActivityExactlyActiveWithinDays_ScoresMaximum()
    {
        // Arrange
        var repository = HealthCheckTestData.CreateRepository(HealthCheckTestData.Now.AddDays(-30));
        var facts = RepositoryFactsBuilder.Build(repository: repository);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.ActivityLastActivity).NormalizedScore.Should().Be(100);
    }

    [Fact]
    public void Calculate_WhenLastActivityExactlyStaleAfterDays_ScoresMinimum()
    {
        // Arrange
        var repository = HealthCheckTestData.CreateRepository(HealthCheckTestData.Now.AddDays(-180));
        var facts = RepositoryFactsBuilder.Build(repository: repository);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.ActivityLastActivity).NormalizedScore.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenLastActivityBetweenBounds_Interpolates()
    {
        // Arrange
        var repository = HealthCheckTestData.CreateRepository(HealthCheckTestData.Now.AddDays(-105));
        var facts = RepositoryFactsBuilder.Build(repository: repository);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.ActivityLastActivity).NormalizedScore.Should().Be(50);
    }

    [Fact]
    public void Calculate_WhenLastActivityInFuture_ClampsDaysToZero()
    {
        // Arrange
        var repository = HealthCheckTestData.CreateRepository(HealthCheckTestData.Now.AddDays(5));
        var facts = RepositoryFactsBuilder.Build(repository: repository);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.ActivityLastActivity).NormalizedScore.Should().Be(100);
    }

    [Fact]
    public void Calculate_WhenCommitsProvided_UsesLastCommitAt()
    {
        // Arrange
        var repository = HealthCheckTestData.CreateRepository(HealthCheckTestData.Now.AddDays(-180));
        var commits = new CommitActivity(1, null, HealthCheckTestData.Now, new Dictionary<DateOnly, int>());
        var facts = RepositoryFactsBuilder.Build(repository: repository, commits: commits);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.ActivityLastActivity).NormalizedScore.Should().Be(100);
    }

    [Fact]
    public void Calculate_WhenCommitsProvided_CountsOnlyCommitsInsideWindow()
    {
        // Arrange
        var commitsByDay = new Dictionary<DateOnly, int>
        {
            [new DateOnly(2026, 1, 1)] = 2,
            [new DateOnly(2025, 12, 31)] = 5
        };
        var commits = new CommitActivity(7, null, HealthCheckTestData.Now, commitsByDay);
        var facts = RepositoryFactsBuilder.Build(commits: commits);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.ActivityCommitFrequency).RawValue.Should().Be(2);
    }

    [Fact]
    public void Calculate_WhenContributorsIncludeBots_ExcludesBots()
    {
        // Arrange
        var contributors = new Contributor[]
        {
            new(_fixture.Create<string>(), 10, false),
            new(_fixture.Create<string>(), 100, true)
        };
        var facts = RepositoryFactsBuilder.Build(contributors: contributors);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.ActivityContributors).RawValue.Should().Be(1);
    }

    [Fact]
    public void Calculate_WhenReleasesPresent_CountsReleases()
    {
        // Arrange
        var releases = new ReleaseInfo[]
        {
            new(_fixture.Create<string>(), "v1.0.0", HealthCheckTestData.Now.AddDays(-10)),
            new(_fixture.Create<string>(), "v1.1.0", HealthCheckTestData.Now)
        };
        var facts = RepositoryFactsBuilder.Build(releases: releases);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.ActivityReleases).RawValue.Should().Be(2);
    }

    [Fact]
    public void Calculate_WhenMergeRequestsPresent_CountsMergeRequests()
    {
        // Arrange
        var mergeRequests = new MergeRequestInfo[]
        {
            MergeRequest(),
            MergeRequest()
        };
        var facts = RepositoryFactsBuilder.Build(
            collaborationAvailability: DataStatus.Available,
            mergeRequests: mergeRequests);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Activity, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.ActivityMergeRequests).RawValue.Should().Be(2);
    }

    private MergeRequestInfo MergeRequest() => new(
        _fixture.Create<string>(),
        _fixture.Create<string>(),
        MergeRequestState.Merged,
        _fixture.Create<string>(),
        HealthCheckTestData.Now.AddDays(-5),
        HealthCheckTestData.Now,
        null,
        null,
        0);
}
