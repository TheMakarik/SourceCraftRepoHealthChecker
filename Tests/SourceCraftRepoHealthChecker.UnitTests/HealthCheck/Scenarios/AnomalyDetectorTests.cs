using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Scenarios;

public sealed class AnomalyDetectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly AnomalyDetector systemUnderTests = new(
        Options.Create(new AnomalyDetectionOptions
        {
            CommitBurstThreshold = 20,
            CommitBurstWindowDays = 3,
            MergeRequestBurstThreshold = 10,
            MergeRequestBurstWindowDays = 3,
            NewContributorRecentDays = 14,
            InactiveAuthorDays = 90,
            SmallChangeCommitThreshold = 2,
            SmallChangeRecentDays = 14
        }),
        new StubTimeProvider(Now));

    [Fact]
    public void Detect_WhenNewAuthorCommitsBurst_ReturnsCommitBurst()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(contributors:
        [
            new Contributor("alice", 25, false, Now.AddDays(-5), Now.AddDays(-4))
        ]);

        // Act
        var actual = systemUnderTests.Detect(facts);

        // Assert
        actual.Should().Contain(anomaly => anomaly.Kind == ActivityAnomalyKind.CommitBurst && anomaly.AuthorLogin == "alice");
    }

    [Fact]
    public void Detect_WhenInactiveAuthorMakesSmallChanges_ReturnsAnomaly()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(contributors:
        [
            new Contributor("bob", 1, false, Now.AddDays(-200), Now.AddDays(-5))
        ]);

        // Act
        var actual = systemUnderTests.Detect(facts);

        // Assert
        actual.Should().Contain(anomaly => anomaly.Kind == ActivityAnomalyKind.InactiveAuthorSmallChanges && anomaly.AuthorLogin == "bob");
    }

    [Fact]
    public void Detect_WhenMergeRequestBurst_ReturnsAnomaly()
    {
        // Arrange
        var mergeRequests = Enumerable.Range(0, 12)
            .Select(index => new MergeRequestInfo($"mr-{index}", "title", MergeRequestState.Open, "carol", Now.AddDays(-2).AddHours(index), null, null, null, 0))
            .ToArray();
        var facts = RepositoryFactsBuilder.Build(mergeRequests: mergeRequests);

        // Act
        var actual = systemUnderTests.Detect(facts);

        // Assert
        actual.Should().Contain(anomaly => anomaly.Kind == ActivityAnomalyKind.MergeRequestBurst && anomaly.AuthorLogin == "carol");
    }

    [Fact]
    public void Detect_WhenHealthyActivity_ReturnsNoAnomalies()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(contributors:
        [
            new Contributor("dave", 3, false, Now.AddDays(-400), Now.AddDays(-100))
        ]);

        // Act
        var actual = systemUnderTests.Detect(facts);

        // Assert
        actual.Should().BeEmpty();
    }
}
