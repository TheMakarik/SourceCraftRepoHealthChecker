using FakeItEasy;
using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestData;
using SourceCraftRepoHealthChecker.UnitTests.HealthCheck.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.HealthCheck.Services;

public sealed class HealthCheckEngineTests
{
    private readonly ICategoryScoreCalculator _categoryScoreCalculator = A.Fake<ICategoryScoreCalculator>();
    private readonly IHealthScoreCalculator _healthScoreCalculator = A.Fake<IHealthScoreCalculator>();
    private readonly IRecommendationGenerator _recommendationGenerator = A.Fake<IRecommendationGenerator>();
    private readonly HealthCheckEngine systemUnderTests;

    public HealthCheckEngineTests()
    {
        var options = HealthCheckTestData.CreateOptionsWrapper();
        var timeProvider = new StubTimeProvider(HealthCheckTestData.Now);

        systemUnderTests = new HealthCheckEngine(
            options,
            _categoryScoreCalculator,
            _healthScoreCalculator,
            _recommendationGenerator,
            timeProvider);

        IReadOnlyCollection<RecommendationDraft> recommendations = [];
        A.CallTo(() => _categoryScoreCalculator.Calculate(A<ScoreCategory>._, A<RepositoryFacts>._))
            .ReturnsLazily((ScoreCategory category, RepositoryFacts facts) =>
                new CategoryScoreResult(category, 50, 1, DataStatus.Available, []));
        A.CallTo(() => _healthScoreCalculator.Calculate(A<IReadOnlyCollection<CategoryScoreResult>>._)).Returns(42);
        A.CallTo(() => _recommendationGenerator.Generate(A<IReadOnlyCollection<CategoryScoreResult>>._)).Returns(recommendations);
    }

    [Fact]
    public async Task CheckAsync_ReturnsAllSixCategories()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build();

        // Act
        var actual = await systemUnderTests.CheckAsync(facts, CancellationToken.None);

        // Assert
        actual.Categories.Should().HaveCount(6);
        actual.Categories.Select(x => x.Category).Should().BeEquivalentTo(Enum.GetValues<ScoreCategory>());
    }

    [Fact]
    public async Task CheckAsync_UsesMethodologyVersionFromOptions()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build();

        // Act
        var actual = await systemUnderTests.CheckAsync(facts, CancellationToken.None);

        // Assert
        actual.MethodologyVersion.Should().Be("test-methodology-2026.1");
    }

    [Fact]
    public async Task CheckAsync_SetsCalculatedAtFromTimeProvider()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build();

        // Act
        var actual = await systemUnderTests.CheckAsync(facts, CancellationToken.None);

        // Assert
        actual.CalculatedAt.Should().Be(HealthCheckTestData.Now);
    }

    [Fact]
    public async Task CheckAsync_ReturnsScoreFromHealthScoreCalculator()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build();

        // Act
        var actual = await systemUnderTests.CheckAsync(facts, CancellationToken.None);

        // Assert
        actual.Score.Should().Be(42);
    }

    [Fact]
    public async Task CheckAsync_ReturnsRecommendationsFromGenerator()
    {
        // Arrange
        var expected = new RecommendationDraft(RecommendationPriority.Critical, "problem", "why", "evidence", "action", "impact", "source");
        A.CallTo(() => _recommendationGenerator.Generate(A<IReadOnlyCollection<CategoryScoreResult>>._))
            .Returns(new[] { expected });
        var facts = RepositoryFactsBuilder.Build();

        // Act
        var actual = await systemUnderTests.CheckAsync(facts, CancellationToken.None);

        // Assert
        actual.Recommendations.Should().ContainSingle().Which.Should().Be(expected);
    }

    [Fact]
    public async Task CheckAsync_PassesFactsToCategoryCalculator()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build();

        // Act
        await systemUnderTests.CheckAsync(facts, CancellationToken.None);

        // Assert
        A.CallTo(() => _categoryScoreCalculator.Calculate(A<ScoreCategory>._, facts)).MustHaveHappened();
    }

    [Fact]
    public async Task CheckAsync_WhenTokenAlreadyCancelled_ThrowsOperationCanceled()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build();
        var cancellationToken = new CancellationToken(canceled: true);

        // Act
        var act = () => systemUnderTests.CheckAsync(facts, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CheckAsync_WhenTokenAlreadyCancelled_DoesNotCalculateCategories()
    {
        // Arrange
        var facts = RepositoryFactsBuilder.Build();
        var cancellationToken = new CancellationToken(canceled: true);

        // Act
        var act = () => systemUnderTests.CheckAsync(facts, cancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        A.CallTo(() => _categoryScoreCalculator.Calculate(A<ScoreCategory>._, A<RepositoryFacts>._)).MustNotHaveHappened();
    }
}
