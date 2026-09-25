using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Services;

public sealed class HealthScoreCalculatorTests
{
    private readonly HealthScoreCalculator systemUnderTests;

    public HealthScoreCalculatorTests()
    {
        systemUnderTests = new HealthScoreCalculator(HealthCheckTestData.CreateOptionsWrapper());
    }

    [Fact]
    public void Calculate_WhenCategoriesEmpty_ReturnsMinimumScore()
    {
        // Arrange
        var categories = Array.Empty<CategoryScoreResult>();

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenAllCategoriesNoData_ReturnsMinimumScore()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Security, 0, 20, DataStatus.NoData),
            Category(ScoreCategory.CodeHealth, 0, 20, DataStatus.NoData)
        };

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenAllCategoriesUnavailable_ReturnsMinimumScore()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Activity, 0, 15, DataStatus.Unavailable),
            Category(ScoreCategory.Issues, 0, 15, DataStatus.Unavailable)
        };

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void Calculate_WithMixOfAvailableAndNoData_UsesOnlyAvailableWeight()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Security, 80, 20, DataStatus.Available),
            Category(ScoreCategory.CodeHealth, 0, 80, DataStatus.NoData)
        };

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(80);
    }

    [Fact]
    public void Calculate_WhenAvailableWeightIsZero_ReturnsMinimumScore()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Security, 90, 0, DataStatus.Available)
        };

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenTotalAvailableWeightIsNegative_ReturnsMinimumScore()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Security, 90, -5, DataStatus.Available)
        };

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void Calculate_WhenWeightedAverageEndsWithHalf_RoundsAwayFromZero()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Security, 63, 1, DataStatus.Available),
            Category(ScoreCategory.CodeHealth, 62, 1, DataStatus.Available)
        };

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(63);
    }

    [Fact]
    public void Calculate_WhenCategoriesHaveDifferentWeights_WeightsScores()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Security, 100, 20, DataStatus.Available),
            Category(ScoreCategory.CodeHealth, 0, 20, DataStatus.Available)
        };

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(50);
    }

    [Fact]
    public void Calculate_WhenWeightedScoreAboveMaximum_ClampsToMaximumScore()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Security, 120, 1, DataStatus.Available),
            Category(ScoreCategory.CodeHealth, 120, 1, DataStatus.Available)
        };

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(100);
    }

    [Fact]
    public void Calculate_WhenWeightedScoreBelowMinimum_ClampsToMinimumScore()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Security, -50, 1, DataStatus.Available)
        };

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void Calculate_WithSingleAvailableCategory_ReturnsItsScore()
    {
        // Arrange
        var categories = new CategoryScoreResult[]
        {
            Category(ScoreCategory.Security, 77, 20, DataStatus.Available)
        };

        // Act
        var actual = systemUnderTests.Calculate(categories);

        // Assert
        actual.Should().Be(77);
    }

    private static CategoryScoreResult Category(ScoreCategory category, int score, double weight, DataStatus status) =>
        new(category, score, weight, status, []);
}
