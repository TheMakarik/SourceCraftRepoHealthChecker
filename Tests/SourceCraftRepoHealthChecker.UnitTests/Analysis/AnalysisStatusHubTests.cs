using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.Analysis;
using SourceCraftRepoHealthChecker.infrastructure.Analysis;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.Analysis;

public sealed class AnalysisStatusHubTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Publish_WhenSubscribed_SubscriberReceivesEvent()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        var received = new List<AnalysisStatusEvent>();
        using var subscription = systemUnderTests.Subscribe(statusEvent =>
        {
            received.Add(statusEvent);
            return true;
        });

        // Act
        systemUnderTests.Publish("repo-1", AnalysisStatuses.Running, null);

        // Assert
        received.Should().ContainSingle();
        received[0].RepositoryId.Should().Be("repo-1");
        received[0].Status.Should().Be(AnalysisStatuses.Running);
        received[0].Score.Should().BeNull();
        received[0].UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void GetSnapshot_WhenRepositoryPublishedMultipleTimes_KeepsLastEvent()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        systemUnderTests.Publish("repo-1", AnalysisStatuses.Running, null);
        systemUnderTests.Publish("repo-1", AnalysisStatuses.Completed, 87);
        systemUnderTests.Publish("repo-2", AnalysisStatuses.Failed, null);

        // Assert
        var snapshot = systemUnderTests.GetSnapshot();
        snapshot.Should().HaveCount(2);
        snapshot.Should().ContainSingle(statusEvent => statusEvent.RepositoryId == "repo-1")
            .Which.Status.Should().Be(AnalysisStatuses.Completed);
        snapshot.Should().ContainSingle(statusEvent => statusEvent.RepositoryId == "repo-2")
            .Which.Score.Should().BeNull();
    }

    [Fact]
    public void Subscribe_WhenDisposed_DoesNotReceiveEvents()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        var received = new List<AnalysisStatusEvent>();
        var subscription = systemUnderTests.Subscribe(statusEvent =>
        {
            received.Add(statusEvent);
            return true;
        });

        // Act
        subscription.Dispose();
        systemUnderTests.Publish("repo-1", AnalysisStatuses.Running, null);

        // Assert
        received.Should().BeEmpty();
    }

    private static AnalysisStatusHub CreateSystemUnderTests() => new(new StubTimeProvider(Now));
}
