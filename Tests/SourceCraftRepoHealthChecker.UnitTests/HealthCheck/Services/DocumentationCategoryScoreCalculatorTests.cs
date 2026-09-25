using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Services;

public sealed class DocumentationCategoryScoreCalculatorTests
{
    private readonly CategoryScoreCalculator systemUnderTests;

    public DocumentationCategoryScoreCalculatorTests()
    {
        var options = HealthCheckTestData.CreateOptionsWrapper();
        var normalizer = new MetricNormalizer(options);
        var timeProvider = new StubTimeProvider(HealthCheckTestData.Now);

        systemUnderTests = new CategoryScoreCalculator(options, normalizer, timeProvider);
    }

    [Fact]
    public void Calculate_WhenDocumentationUnavailable_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(documentationAvailability: DataStatus.Unavailable);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Documentation, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
        actual.Score.Should().Be(0);
        actual.Metrics.Should().BeEmpty();
        actual.Weight.Should().Be(15);
    }

    [Fact]
    public void Calculate_WhenDocumentationNoData_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(documentationAvailability: DataStatus.NoData);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Documentation, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
    }

    [Fact]
    public void Calculate_WhenAvailabilityAvailableButReportNull_ReturnsNoData()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build(documentation: null);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Documentation, facts);

        // Assert
        actual.DataStatus.Should().Be(DataStatus.NoData);
    }

    [Fact]
    public void Calculate_WhenAllDocumentationPresent_ReturnsMaximumScore()
    {
        // Arrange
        var report = new DocumentationReport(true, true, true, true, true, true);
        var facts = RepositoryFactsBuilder.Build(documentation: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Documentation, facts);

        // Assert
        actual.Score.Should().Be(100);
        actual.DataStatus.Should().Be(DataStatus.Available);
        actual.Metrics.Should().HaveCount(6);
    }

    [Fact]
    public void Calculate_WhenAllDocumentationAbsent_ReturnsMinimumScore()
    {
        // Arrange
        var report = new DocumentationReport(false, false, false, false, false, false);
        var facts = RepositoryFactsBuilder.Build(documentation: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Documentation, facts);

        // Assert
        actual.Score.Should().Be(0);
        actual.DataStatus.Should().Be(DataStatus.Available);
    }

    [Fact]
    public void Calculate_WhenOnlyReadmePresent_WeightsMetrics()
    {
        // Arrange
        var report = new DocumentationReport(true, false, false, false, false, false);
        var facts = RepositoryFactsBuilder.Build(documentation: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Documentation, facts);

        // Assert
        actual.Score.Should().Be(33);
    }

    [Fact]
    public void Calculate_WhenReadmeAndLicensePresent_WeightsMetrics()
    {
        // Arrange
        var report = new DocumentationReport(true, true, false, false, false, false);
        var facts = RepositoryFactsBuilder.Build(documentation: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Documentation, facts);

        // Assert
        actual.Score.Should().Be(56);
    }

    [Fact]
    public void Calculate_WhenDocumentationAvailable_UsesDocumentationCategoryWeight()
    {
        // Arrange
        var report = new DocumentationReport(true, true, true, true, true, true);
        var facts = RepositoryFactsBuilder.Build(documentation: report);

        // Act
        var actual = systemUnderTests.Calculate(ScoreCategory.Documentation, facts);

        // Assert
        actual.Weight.Should().Be(15);
    }
}
