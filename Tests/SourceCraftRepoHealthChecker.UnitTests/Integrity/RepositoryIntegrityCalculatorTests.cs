using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.Integrity.Models;
using SourceCraftRepoHealthChecker.Application.Integrity.Options;
using SourceCraftRepoHealthChecker.Application.Integrity.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.Integrity;

public sealed class RepositoryIntegrityCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly IAnomalyDetector _anomalyDetector = A.Fake<IAnomalyDetector>();

    public RepositoryIntegrityCalculatorTests()
    {
        A.CallTo(() => _anomalyDetector.Detect(A<RepositoryFacts>._)).Returns([]);
    }

    [Fact]
    public void Calculate_WhenNoSignals_ReturnsOkWithFullScore()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        var facts = RepositoryFactsBuilder.Build();
        var input = new RepositoryIntegrityInput(facts, 0, Now, []);

        // Act
        var actual = systemUnderTests.Calculate(input);

        // Assert
        actual.Status.Should().Be(RepositoryIntegrityStatuses.Ok);
        actual.Score.Should().Be(100);
        actual.Signals.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_WhenLikesWithoutActivity_AddsSuspicionSignal()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        var facts = RepositoryFactsBuilder.Build();
        var input = new RepositoryIntegrityInput(facts, 50, Now.AddDays(-400), []);

        // Act
        var actual = systemUnderTests.Calculate(input);

        // Assert
        actual.Status.Should().Be(RepositoryIntegrityStatuses.Check);
        actual.Signals.Should().ContainSingle(signal => signal.Contains("накрутка"));
    }

    [Fact]
    public void Calculate_WhenCommitsMissing_AddsEmptyActivitySignal()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        var facts = RepositoryFactsBuilder.Build(commits: new CommitActivity(0, null, null, new Dictionary<DateOnly, int>()));
        var input = new RepositoryIntegrityInput(facts, 0, Now, []);

        // Act
        var actual = systemUnderTests.Calculate(input);

        // Assert
        actual.Signals.Should().ContainSingle(signal => signal.Contains("нет коммитов"));
    }

    [Fact]
    public void Calculate_WhenDetectorReturnsAnomaly_AddsAnomalySignal()
    {
        // Arrange
        var anomaly = new ActivityAnomaly(ActivityAnomalyKind.CommitBurst, "alice", 25, Now.AddDays(-5), Now.AddDays(-4), "25 коммитов за короткий промежуток");
        A.CallTo(() => _anomalyDetector.Detect(A<RepositoryFacts>._)).Returns([anomaly]);
        var systemUnderTests = CreateSystemUnderTests();
        var facts = RepositoryFactsBuilder.Build();
        var input = new RepositoryIntegrityInput(facts, 0, Now, []);

        // Act
        var actual = systemUnderTests.Calculate(input);

        // Assert
        actual.Signals.Should().Contain("25 коммитов за короткий промежуток");
    }

    [Fact]
    public void Calculate_WhenDetectorAndPersistedAnomalyMatch_KeepsSingleSignal()
    {
        // Arrange
        var anomaly = new ActivityAnomaly(ActivityAnomalyKind.CommitBurst, "alice", 25, Now.AddDays(-5), Now.AddDays(-4), "дубликат");
        A.CallTo(() => _anomalyDetector.Detect(A<RepositoryFacts>._)).Returns([anomaly]);
        var systemUnderTests = CreateSystemUnderTests();
        var facts = RepositoryFactsBuilder.Build();
        var input = new RepositoryIntegrityInput(facts, 0, Now, ["дубликат"]);

        // Act
        var actual = systemUnderTests.Calculate(input);

        // Assert
        actual.Signals.Should().ContainSingle(signal => signal == "дубликат");
    }

    [Fact]
    public void Calculate_WhenMostContributorsMadeTinyChanges_AddsTinyCommitsSignal()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();
        var contributors = new Contributor[]
        {
            new("alice", 1, false),
            new("bob", 1, false),
            new("carol", 2, false),
            new("dave", 200, false)
        };
        var facts = RepositoryFactsBuilder.Build(
            commits: new CommitActivity(204, Now.AddDays(-500), Now.AddDays(-1), new Dictionary<DateOnly, int>()),
            contributors: contributors);
        var input = new RepositoryIntegrityInput(facts, 0, Now, []);

        // Act
        var actual = systemUnderTests.Calculate(input);

        // Assert
        actual.Signals.Should().Contain(signal => signal.Contains("точечной"));
    }

    private RepositoryIntegrityCalculator CreateSystemUnderTests() =>
        new(_anomalyDetector, Options.Create(new RepositoryIntegrityOptions()), new StubTimeProvider(Now));
}
