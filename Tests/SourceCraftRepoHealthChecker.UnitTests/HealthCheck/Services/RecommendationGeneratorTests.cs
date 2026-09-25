using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Services;

public sealed class RecommendationGeneratorTests
{
    private readonly RecommendationGenerator systemUnderTests;

    public RecommendationGeneratorTests()
    {
        systemUnderTests = new RecommendationGenerator(HealthCheckTestData.CreateOptionsWrapper());
    }

    [Fact]
    public void Generate_WhenCategoryAtMinimumAcceptableScore_ProducesNoRecommendation()
    {
        // Arrange
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.Security, 70) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void Generate_WhenCategoryAboveMinimumAcceptableScore_ProducesNoRecommendation()
    {
        // Arrange
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.Security, 90) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void Generate_WhenCategoryBelowMinimumAndAboveHighPriority_IsMedium()
    {
        // Arrange
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.Documentation, 60) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().ContainSingle().Which.Priority.Should().Be(RecommendationPriority.Medium);
    }

    [Fact]
    public void Generate_WhenCategoryAtHighPriorityScore_IsMedium()
    {
        // Arrange
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.Documentation, 50) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().ContainSingle().Which.Priority.Should().Be(RecommendationPriority.Medium);
    }

    [Fact]
    public void Generate_WhenCategoryBetweenCriticalAndHighPriority_IsHigh()
    {
        // Arrange
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.Activity, 49) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().ContainSingle().Which.Priority.Should().Be(RecommendationPriority.High);
    }

    [Fact]
    public void Generate_WhenCategoryAtCriticalPriorityScore_IsHigh()
    {
        // Arrange
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.Activity, 30) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().ContainSingle().Which.Priority.Should().Be(RecommendationPriority.High);
    }

    [Fact]
    public void Generate_WhenCategoryBelowCriticalPriorityScore_IsCritical()
    {
        // Arrange
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.Activity, 29) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().ContainSingle().Which.Priority.Should().Be(RecommendationPriority.Critical);
    }

    [Fact]
    public void Generate_WhenSecurityNoData_ProducesLowSecurityNotice()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            new(ScoreCategory.Security, 0, 20, DataStatus.NoData, [])
        };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        var recommendation = actual.Single();
        recommendation.Priority.Should().Be(RecommendationPriority.Low);
        recommendation.Problem.Should().Contain("Подключите AppSec SourceCraft");
        recommendation.Action.Should().Contain("AppSec SourceCraft");
        recommendation.SourceReference.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Generate_WhenSecurityUnavailable_ProducesLowSecurityNotice()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            new(ScoreCategory.Security, 0, 20, DataStatus.Unavailable, [])
        };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Single().Priority.Should().Be(RecommendationPriority.Low);
    }

    [Fact]
    public void Generate_WhenNonSecurityCategoryNoData_ProducesNoRecommendation()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            new(ScoreCategory.Activity, 0, 15, DataStatus.NoData, [])
        };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void Generate_WhenNonSecurityCategoryUnavailable_ProducesNoRecommendation()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            new(ScoreCategory.Activity, 0, 15, DataStatus.Unavailable, [])
        };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void Generate_WhenMultipleCategoriesBelowMinimum_CreatesRecommendationForEach()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Security, 10),
            Category(ScoreCategory.CodeHealth, 40),
            Category(ScoreCategory.CiCd, 60)
        };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().HaveCount(3);
    }

    [Fact]
    public void Generate_WhenSecurityCategoryLow_BuildsProblemFromMetricFacts()
    {
        // Arrange
        var metrics = new MetricScore[]
        {
            Metric(MetricCode.SecurityCriticalFindings, 2, 60, 20),
            Metric(MetricCode.SecurityHighFindings, 3, 70, 10)
        };
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.Security, 10, metrics) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        var recommendation = actual.Single();
        recommendation.Problem.Should().Contain("критических 2").And.Contain("высоких 3");
        recommendation.SourceReference.Should().Contain(nameof(MetricCode.SecurityCriticalFindings));
    }

    [Fact]
    public void Generate_WhenCodeHealthCategoryLow_BuildsProblemFromMetricFacts()
    {
        // Arrange
        var metrics = new MetricScore[]
        {
            Metric(MetricCode.CodeHealthTodo, 47, 53, 1),
            Metric(MetricCode.CodeHealthFixme, 12, 76, 2),
            Metric(MetricCode.CodeHealthStaleComments, 200, 95, 5)
        };
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.CodeHealth, 40, metrics) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        var recommendation = actual.Single();
        recommendation.Problem.Should().Contain("47").And.Contain("12").And.Contain("200");
        recommendation.SourceReference.Should().Contain(nameof(MetricCode.CodeHealthTodo));
    }

    [Fact]
    public void Generate_WhenDocumentationCategoryLow_ListsMissingDocuments()
    {
        // Arrange
        var metrics = new MetricScore[]
        {
            Metric(MetricCode.DocumentationReadme, 1, 100, 3),
            Metric(MetricCode.DocumentationLicense, 0, 0, 2)
        };
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.Documentation, 10, metrics) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        var recommendation = actual.Single();
        recommendation.Problem.Should().Contain("лицензия");
        recommendation.SourceReference.Should().Contain(nameof(MetricCode.DocumentationLicense));
    }

    [Fact]
    public void Generate_WhenCategoryLow_SetsExpectedImpactAndSourceReference()
    {
        // Arrange
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.CiCd, 10) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        var recommendation = actual.Single();
        recommendation.ExpectedImpact.Should().Contain("70");
        recommendation.SourceReference.Should().NotBeNullOrWhiteSpace();
    }

    private static CategoryScoreResult Category(ScoreCategory category, int score, IReadOnlyCollection<MetricScore>? metrics = null) =>
        new(category, score, 15, DataStatus.Available, metrics ?? []);

    private static MetricScore Metric(MetricCode code, double rawValue, double normalizedScore, double weight) =>
        new(code, rawValue, normalizedScore, weight, DataStatus.Available);
}
