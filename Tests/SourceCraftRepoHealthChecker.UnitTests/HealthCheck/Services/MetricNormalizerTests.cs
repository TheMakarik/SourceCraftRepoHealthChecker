using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Services;

public sealed class MetricNormalizerTests
{
    [Fact]
    public void Normalize_WhenWorstEqualsBestAndValueEqualsBest_ReturnsMaximumScore()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var actual = systemUnderTests.Normalize(5, 5, 5);

        // Assert
        actual.Should().Be(100);
    }

    [Fact]
    public void Normalize_WhenWorstEqualsBestAndValueDiffers_ReturnsMinimumScore()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var actual = systemUnderTests.Normalize(4, 5, 5);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void Normalize_WhenValueEqualsWorst_ReturnsMinimumScore()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var actual = systemUnderTests.Normalize(0, 0, 100);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void Normalize_WhenValueEqualsBest_ReturnsMaximumScore()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var actual = systemUnderTests.Normalize(100, 0, 100);

        // Assert
        actual.Should().Be(100);
    }

    [Fact]
    public void Normalize_WhenValueInMiddle_ReturnsHalfOfScale()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var actual = systemUnderTests.Normalize(50, 0, 100);

        // Assert
        actual.Should().Be(50);
    }

    [Fact]
    public void Normalize_WhenValueBelowWorst_ClampsToMinimumScore()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var actual = systemUnderTests.Normalize(-10, 0, 100);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void Normalize_WhenValueAboveBest_ClampsToMaximumScore()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var actual = systemUnderTests.Normalize(150, 0, 100);

        // Assert
        actual.Should().Be(100);
    }

    [Fact]
    public void Normalize_WithReversedBounds_InterpolatesDescending()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var atWorst = systemUnderTests.Normalize(100, 100, 0);
        var atBest = systemUnderTests.Normalize(0, 100, 0);
        var atMiddle = systemUnderTests.Normalize(50, 100, 0);

        // Assert
        atWorst.Should().Be(0);
        atBest.Should().Be(100);
        atMiddle.Should().Be(50);
    }

    [Fact]
    public void Normalize_WithReversedBoundsAndValueBelowRange_ClampsToMinimumScore()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var actual = systemUnderTests.Normalize(120, 100, 0);

        // Assert
        actual.Should().Be(0);
    }

    [Fact]
    public void Normalize_WithCustomScale_ScalesWithinMinimumAndMaximum()
    {
        // Arrange
        var scoreScale = new ScoreScaleOptions { MinimumScore = 10, MaximumScore = 90 };
        var systemUnderTests = CreateSystemUnderTests(scoreScale);

        // Act
        var atWorst = systemUnderTests.Normalize(0, 0, 100);
        var atBest = systemUnderTests.Normalize(100, 0, 100);
        var atMiddle = systemUnderTests.Normalize(50, 0, 100);

        // Assert
        atWorst.Should().Be(10);
        atBest.Should().Be(90);
        atMiddle.Should().Be(50);
    }

    private static MetricNormalizer CreateSystemUnderTests(ScoreScaleOptions? scoreScale = null)
    {
        var options = HealthCheckTestData.CreateOptionsWrapper(HealthCheckTestData.CreateOptions(scoreScale: scoreScale));

        return new MetricNormalizer(options);
    }
}
