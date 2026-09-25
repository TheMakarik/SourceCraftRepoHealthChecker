using AutoFixture;
using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Services;

public sealed class SecurityCategoryScoreCalculatorTests
{
    private readonly Fixture _fixture = new();
    private readonly CategoryScoreCalculator systemUnderTests;

    public SecurityCategoryScoreCalculatorTests()
    {
        var options = HealthCheckTestData.CreateOptionsWrapper();
        var normalizer = new MetricNormalizer(options);
        var timeProvider = new StubTimeProvider(HealthCheckTestData.Now);

        systemUnderTests = new CategoryScoreCalculator(options, normalizer, timeProvider);
    }

    [Fact]
    public void Calculate_WhenSecurityUnavailable_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(securityAvailability: DataStatus.Unavailable);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Security, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
        actual.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_WhenSecurityNoData_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(securityAvailability: DataStatus.NoData);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Security, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
    }

    [Fact]
    public void Calculate_WhenNoFindings_ReturnsMaximumScore()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(findings: []);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Security, facts);

        // Assert
        actual.Score.Should().Be(100);
        actual.DataStatus.Should().Be(DataStatus.Available);
        actual.Metrics.Should().HaveCount(5);
    }

    [Fact]
    public void Calculate_WhenOpenFindingsAcrossSeverities_SubtractsPenalties()
    {
        // Arrange
        var findings = new SecurityFinding[]
        {
            Finding(SecurityFindingKind.Sast, SecuritySeverity.Critical, SecurityFindingStatus.Open),
            Finding(SecurityFindingKind.Sca, SecuritySeverity.High, SecurityFindingStatus.Open),
            Finding(SecurityFindingKind.SecretScanning, SecuritySeverity.Medium, SecurityFindingStatus.Open),
            Finding(SecurityFindingKind.Sast, SecuritySeverity.Low, SecurityFindingStatus.Open)
        };
        var facts = RepositoryFactsBuilder.Build(findings: findings);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Security, facts);

        // Assert
        actual.Score.Should().Be(86);
    }

    [Fact]
    public void Calculate_WhenFixedFindingsPresent_ReportsFixedWithoutChangingScore()
    {
        // Arrange
        var findings = new SecurityFinding[]
        {
            Finding(SecurityFindingKind.Sast, SecuritySeverity.Critical, SecurityFindingStatus.Open),
            Finding(SecurityFindingKind.Sca, SecuritySeverity.Low, SecurityFindingStatus.Fixed),
            Finding(SecurityFindingKind.Sca, SecuritySeverity.Low, SecurityFindingStatus.Fixed)
        };
        var facts = RepositoryFactsBuilder.Build(findings: findings);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Security, facts);

        // Assert
        actual.Score.Should().Be(89);
        actual.Metrics.Single(x => x.Code == MetricCode.SecurityFixedFindings).RawValue.Should().Be(2);
    }

    [Fact]
    public void Calculate_WhenManyCriticalFindings_ReducesScore()
    {
        // Arrange
        var findings = Enumerable.Range(0, 10)
            .Select(_ => Finding(SecurityFindingKind.Sast, SecuritySeverity.Critical, SecurityFindingStatus.Open))
            .ToArray();
        var facts = RepositoryFactsBuilder.Build(findings: findings);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Security, facts);

        // Assert
        actual.Score.Should().Be(46);
    }

    [Fact]
    public void Calculate_WhenFindingsIgnored_DoesNotAffectScore()
    {
        // Arrange
        var findings = new SecurityFinding[]
        {
            Finding(SecurityFindingKind.Sast, SecuritySeverity.Critical, SecurityFindingStatus.Ignored)
        };
        var facts = RepositoryFactsBuilder.Build(findings: findings);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Security, facts);

        // Assert
        actual.Score.Should().Be(100);
    }

    [Fact]
    public void Calculate_WhenOnlyFixedFindingsPresent_KeepsMaximumScore()
    {
        // Arrange
        var findings = new SecurityFinding[]
        {
            Finding(SecurityFindingKind.Sca, SecuritySeverity.Low, SecurityFindingStatus.Fixed)
        };
        var facts = RepositoryFactsBuilder.Build(findings: findings);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Security, facts);

        // Assert
        actual.Score.Should().Be(100);
    }

    [Fact]
    public void Calculate_WhenFindingsPresent_ReportsSeverityCounts()
    {
        // Arrange
        var findings = new SecurityFinding[]
        {
            Finding(SecurityFindingKind.Sast, SecuritySeverity.Critical, SecurityFindingStatus.Open),
            Finding(SecurityFindingKind.Sast, SecuritySeverity.Critical, SecurityFindingStatus.Open),
            Finding(SecurityFindingKind.Sca, SecuritySeverity.High, SecurityFindingStatus.Open),
            Finding(SecurityFindingKind.SecretScanning, SecuritySeverity.Medium, SecurityFindingStatus.Open),
            Finding(SecurityFindingKind.Sca, SecuritySeverity.Low, SecurityFindingStatus.Fixed)
        };
        var facts = RepositoryFactsBuilder.Build(findings: findings);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Security, facts);

        // Assert
        actual.Metrics.Single(x => x.Code == MetricCode.SecurityCriticalFindings).RawValue.Should().Be(2);
        actual.Metrics.Single(x => x.Code == MetricCode.SecurityHighFindings).RawValue.Should().Be(1);
        actual.Metrics.Single(x => x.Code == MetricCode.SecurityMediumFindings).RawValue.Should().Be(1);
        actual.Metrics.Single(x => x.Code == MetricCode.SecurityLowFindings).RawValue.Should().Be(0);
        actual.Metrics.Single(x => x.Code == MetricCode.SecurityFixedFindings).RawValue.Should().Be(1);
    }

    [Fact]
    public void Calculate_WhenMixedFindings_ScoreEqualsWeightedAverageOfMetrics()
    {
        // Arrange
        var findings = new SecurityFinding[]
        {
            Finding(SecurityFindingKind.Sast, SecuritySeverity.Critical, SecurityFindingStatus.Open),
            Finding(SecurityFindingKind.Sast, SecuritySeverity.High, SecurityFindingStatus.Open),
            Finding(SecurityFindingKind.Sast, SecuritySeverity.Medium, SecurityFindingStatus.Open)
        };
        var facts = RepositoryFactsBuilder.Build(findings: findings);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Security, facts);

        // Assert
        var weighted = WeightedAverage(actual.Metrics);
        actual.Score.Should().Be((int)Math.Round(weighted, MidpointRounding.AwayFromZero));
    }

    private static double WeightedAverage(IReadOnlyCollection<MetricScore> metrics)
    {
        var weighted = metrics.Where(x => x.Weight > 0).ToArray();

        return weighted.Sum(x => x.NormalizedScore * x.Weight) / weighted.Sum(x => x.Weight);
    }

    private SecurityFinding Finding(SecurityFindingKind kind, SecuritySeverity severity, SecurityFindingStatus status) =>
        new(_fixture.Create<string>(), kind, severity, status, _fixture.Create<string>(), null, null);
}
