using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Scheduling;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;
using SourceCraftRepoHealthChecker.infrastructure.Scheduling;

namespace SourceCraftRepoHealthChecker.UnitTests.Scheduling;

public sealed class AnalysisWorkerTests
{
    private readonly IAnalyzeRepositoryUseCase _analyzeRepositoryUseCase = A.Fake<IAnalyzeRepositoryUseCase>();
    private readonly ILogger<AnalysisWorker> _logger = A.Dummy<ILogger<AnalysisWorker>>();

    [Fact]
    public async Task ProcessAsync_WhenAnalysisSucceeds_ReturnsTrue()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxAttempts: 3);

        // Act
        var actual = await systemUnderTests.ProcessAsync(new AnalysisJob("repo-1"), CancellationToken.None);

        // Assert
        actual.Should().BeTrue();
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ProcessAsync_WhenFirstAttemptFails_RetriesAndSucceeds()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxAttempts: 3);
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("boom")).Once()
            .Then.Returns(Task.FromResult(A.Dummy<AnalyzeRepositoryResult>()));

        // Act
        var actual = await systemUnderTests.ProcessAsync(new AnalysisJob("repo-1"), CancellationToken.None);

        // Assert
        actual.Should().BeTrue();
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._)).MustHaveHappened(2, Times.Exactly);
    }

    [Fact]
    public async Task ProcessAsync_WhenAlwaysFails_ReturnsFalseAfterMaxAttempts()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxAttempts: 2);
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var actual = await systemUnderTests.ProcessAsync(new AnalysisJob("repo-1"), CancellationToken.None);

        // Assert
        actual.Should().BeFalse();
        A.CallTo(() => _analyzeRepositoryUseCase.AnalyzeAsync(A<AnalyzeRepositoryRequest>._, A<CancellationToken>._)).MustHaveHappened(2, Times.Exactly);
    }

    private AnalysisWorker CreateSystemUnderTests(int maxAttempts)
    {
        var options = A.Fake<IOptions<ScalingOptions>>();
        A.CallTo(() => options.Value).Returns(new ScalingOptions
        {
            SchedulerEnabled = true,
            WorkerEnabled = true,
            WorkerConcurrency = 1,
            WorkerMaxAttempts = maxAttempts,
            WorkerRetryDelayMilliseconds = 0,
            QueueCapacity = 10
        });

        return new AnalysisWorker(_analyzeRepositoryUseCase, options, _logger);
    }
}
