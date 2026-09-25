using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Scenarios;

public sealed class MethodologyControlScenariosTests
{
    private readonly HealthCheckEngine systemUnderTests;

    public MethodologyControlScenariosTests()
    {
        var options = HealthCheckTestData.CreateOptionsWrapper();
        var timeProvider = new StubTimeProvider(HealthCheckTestData.Now);
        var normalizer = new MetricNormalizer(options);
        var categoryScoreCalculator = new CategoryScoreCalculator(options, normalizer, timeProvider);
        var healthScoreCalculator = new HealthScoreCalculator(options);
        var recommendationGenerator = new RecommendationGenerator(options);

        systemUnderTests = new HealthCheckEngine(options, categoryScoreCalculator, healthScoreCalculator, recommendationGenerator, timeProvider);
    }

    [Fact]
    public async Task Score_WhenRepositoryHealthy_IsHigh()
    {
        // Arrange
        var facts = HealthyFacts();

        // Act
        var actual = await ScoreAsync(facts);

        // Assert
        actual.Should().BeGreaterThanOrEqualTo(95);
    }

    [Fact]
    public async Task Score_WhenCriticalVulnerabilitiesPresent_IsNoticeablyLower()
    {
        // Arrange
        var healthy = await ScoreAsync(HealthyFacts());
        var findings = Enumerable.Range(0, 5).Select(i => Finding($"crit-{i}", SecuritySeverity.Critical))
            .Concat(Enumerable.Range(0, 5).Select(i => Finding($"high-{i}", SecuritySeverity.High)))
            .ToArray();
        var degradedFacts = HealthyFacts(findings: findings);

        // Act
        var degraded = await ScoreAsync(degradedFacts);

        // Assert
        degraded.Should().BeLessThan(healthy);
        (healthy - degraded).Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Score_WhenTechnicalDebtGrows_IsNoticeablyLower()
    {
        // Arrange
        var healthy = await ScoreAsync(HealthyFacts());
        var debt = new CodeHealthReport(100, 100, 200, TimeSpan.FromDays(200));
        var debtFacts = HealthyFacts(codeHealth: debt);

        // Act
        var degraded = await ScoreAsync(debtFacts);

        // Assert
        degraded.Should().BeLessThan(healthy);
        (healthy - degraded).Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Score_WhenTechnicalDebtGrows_DoesNotIncrease()
    {
        // Arrange
        int[] todoCounts = [0, 10, 50, 100];
        var scores = new List<int>();

        // Act
        foreach (var todo in todoCounts)
            scores.Add(await ScoreAsync(HealthyFacts(codeHealth: new CodeHealthReport(todo, 0, todo, null))));

        // Assert
        scores.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Score_WhenCategoryUnavailable_IsNotLowerThanPoorCategory()
    {
        // Arrange
        var poorIssues = Enumerable.Range(0, 20)
            .Select(i => new IssueInfo($"issue-{i}", "title", IssueState.Open, "author", HealthCheckTestData.Now.AddDays(-100), null, null, HealthCheckTestData.Now.AddDays(-90)))
            .ToArray();
        var poorFacts = HealthyFacts(issues: poorIssues);
        var unavailableFacts = HealthyFacts(collaborationAvailability: DataStatus.Unavailable);

        // Act
        var poor = await ScoreAsync(poorFacts);
        var unavailable = await ScoreAsync(unavailableFacts);

        // Assert
        unavailable.Should().BeGreaterThanOrEqualTo(poor);
    }

    [Fact]
    public async Task Score_WhenAllSourcesUnavailable_IsMinimum()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(
            activityAvailability: DataStatus.Unavailable,
            collaborationAvailability: DataStatus.Unavailable,
            securityAvailability: DataStatus.Unavailable,
            pipelineAvailability: DataStatus.Unavailable,
            codeHealthAvailability: DataStatus.Unavailable,
            documentationAvailability: DataStatus.Unavailable);

        // Act
        var actual = await ScoreAsync(facts);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public async Task Score_WhenLikesChange_IsUnchanged()
    {
        // Arrange
        var withoutLikes = HealthyFacts(likes: 0);
        var withLikes = HealthyFacts(likes: 1_000_000);

        // Act
        var actualWithoutLikes = await ScoreAsync(withoutLikes);
        var actualWithLikes = await ScoreAsync(withLikes);

        // Assert
        actualWithLikes.Should().Be(actualWithoutLikes);
    }

    [Fact]
    public async Task Score_WhenOnlySecondaryMetricChanges_ImpactIsBounded()
    {
        // Arrange
        var withReleases = await ScoreAsync(HealthyFacts());
        var withoutReleases = await ScoreAsync(HealthyFacts(releases: []));

        // Act
        var impact = withReleases - withoutReleases;

        // Assert
        impact.Should().BeInRange(0, 10);
    }

    [Fact]
    public async Task Score_WhenSameFactsCalculatedTwice_IsReproducible()
    {
        // Arrange
        var facts = HealthyFacts(codeHealth: new CodeHealthReport(37, 11, 48, TimeSpan.FromDays(120)));

        // Act
        var first = await ScoreAsync(facts);
        var second = await ScoreAsync(facts);

        // Assert
        second.Should().Be(first);
    }

    [Fact]
    public async Task Recommendations_WhenRepositoryHealthy_AreEmpty()
    {
        // Arrange
        var facts = HealthyFacts();

        // Act
        var actual = await systemUnderTests.CheckAsync(facts, CancellationToken.None);

        // Assert
        actual.Recommendations.Should().BeEmpty();
    }

    [Fact]
    public async Task Recommendations_WhenProblemsPresent_ReferenceFacts()
    {
        // Arrange
        var findings = Enumerable.Range(0, 3).Select(index => Finding($"crit-{index}", SecuritySeverity.Critical)).ToArray();
        var facts = HealthyFacts(findings: findings);

        // Act
        var actual = await systemUnderTests.CheckAsync(facts, CancellationToken.None);

        // Assert
        actual.Recommendations.Should().Contain(recommendation => recommendation.SourceReference.Contains("Security"));
    }

    [Fact]
    public async Task Recommendations_WhenSecurityUnavailable_ContainAppSecNotice()
    {
        // Arrange
        var facts = HealthyFacts(securityAvailability: DataStatus.Unavailable);

        // Act
        var actual = await systemUnderTests.CheckAsync(facts, CancellationToken.None);

        // Assert
        actual.Recommendations.Should().Contain(recommendation => recommendation.Action.Contains("AppSec"));
    }

    private async Task<int> ScoreAsync(RepositoryFacts facts) =>
        (await systemUnderTests.CheckAsync(facts, CancellationToken.None)).Score;

    private static RepositoryFacts HealthyFacts(
        int likes = 0,
        IReadOnlyCollection<SecurityFinding>? findings = null,
        CodeHealthReport? codeHealth = null,
        IReadOnlyCollection<IssueInfo>? issues = null,
        IReadOnlyCollection<ReleaseInfo>? releases = null,
        DataStatus securityAvailability = DataStatus.Available,
        DataStatus collaborationAvailability = DataStatus.Available)
    {
        var now = HealthCheckTestData.Now;
        var commits = new CommitActivity(30, now.AddDays(-30), now, new Dictionary<DateOnly, int> { [DateOnly.FromDateTime(now.UtcDateTime)] = 30 });
        var contributors = new[]
        {
            new Contributor("alice", 6, false),
            new Contributor("bob", 6, false),
            new Contributor("carol", 6, false),
            new Contributor("dave", 6, false),
            new Contributor("erin", 6, false)
        };
        var resolvedReleases = releases ??
        [
            new ReleaseInfo("v1.0.0", "v1.0.0", now.AddDays(-1)),
            new ReleaseInfo("v1.1.0", "v1.1.0", now.AddDays(-2)),
            new ReleaseInfo("v1.2.0", "v1.2.0", now.AddDays(-3))
        ];
        var pipelineRuns = Enumerable.Range(0, 10)
            .Select(index => new PipelineRun($"run-{index}", PipelineStatus.Success, "main", now, now))
            .ToArray();

        return RepositoryFactsBuilder.Build(
            repository: HealthCheckTestData.CreateRepository() with { LikesCount = likes },
            commits: commits,
            contributors: contributors,
            releases: resolvedReleases,
            collaborationAvailability: collaborationAvailability,
            issues: issues ?? [],
            securityAvailability: securityAvailability,
            findings: findings ?? [],
            pipelineRuns: pipelineRuns,
            codeHealth: codeHealth ?? new CodeHealthReport(0, 0, 0, null),
            documentation: new DocumentationReport(true, true, true, true, true, true));
    }

    private static SecurityFinding Finding(string id, SecuritySeverity severity) =>
        new(id, SecurityFindingKind.Sast, severity, SecurityFindingStatus.Open, "finding", null, null);
}
