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
    public void Generate_WhenCategoryNoData_ProducesNoRecommendation()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            new(ScoreCategory.Security, 0, 20, DataStatus.NoData, [])
        };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void Generate_WhenCategoryUnavailable_ProducesNoRecommendation()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            new(ScoreCategory.Security, 0, 20, DataStatus.Unavailable, [])
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
    public void Generate_WhenCategoryLow_IncludesProblemActionAndImpact()
    {
        // Arrange
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.Security, 10) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        var recommendation = actual.Single();
        recommendation.Problem.Should().Contain(nameof(ScoreCategory.Security)).And.Contain("10");
        recommendation.Action.Should().Contain("AppSec SourceCraft");
        recommendation.ExpectedImpact.Should().Contain("70");
        recommendation.SourceReference.Should().BeNull();
    }

    [Fact]
    public void Generate_WhenCiCdCategoryLow_MapsActionToCiCd()
    {
        // Arrange
        var categories = new CategoryScoreResult[] { Category(ScoreCategory.CiCd, 10) };

        // Act
        var actual = systemUnderTests.Generate(categories);

        // Assert
        actual.Single().Action.Should().Contain("CI");
    }

    private static CategoryScoreResult Category(ScoreCategory category, int score) =>
        new(category, score, 15, DataStatus.Available, []);
}
