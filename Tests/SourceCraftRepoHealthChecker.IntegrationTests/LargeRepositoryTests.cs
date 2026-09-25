using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.infrastructure.SourceCraft;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace SourceCraftRepoHealthChecker.IntegrationTests;

public sealed class LargeRepositoryTests : IDisposable
{
    private const string LargeRepositoryTestsEnvironmentVariable = "SOURCECRAFT_LARGE_REPO_TESTS";
    private const string FilesEnvironmentVariable = "SOURCECRAFT_LARGE_REPO_FILES";
    private const string CommitsEnvironmentVariable = "SOURCECRAFT_LARGE_REPO_COMMITS";

    private static readonly DateTimeOffset FixedNow = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan GatedTimeout = TimeSpan.FromMinutes(60);

    private readonly TempFileSystem _tempFileSystem = new();
    private readonly HealthCheckOptions _healthCheckOptions = HealthCheckOptionsLoader.Load();
    private readonly LocalGitRepositoryReader systemUnderTests = new();
    private readonly ITestOutputHelper _output;

    public LargeRepositoryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose() => _tempFileSystem.Dispose();

    [Fact]
    public async Task ReadAndCheckAsync_WhenModeratelyLargeRepository_CompletesAndProducesScore()
    {
        // Arrange
        var repositoryPath = CreateLargeRepository(files: 2000, commits: 20, authors: 5);

        // Act
        var activity = await systemUnderTests.GetCommitActivityAsync(repositoryPath, CancellationToken.None);
        var contributors = await systemUnderTests.GetContributorsAsync(repositoryPath, CancellationToken.None);
        var releases = await systemUnderTests.GetReleasesAsync(repositoryPath, CancellationToken.None);
        var codeHealth = await systemUnderTests.GetCodeHealthAsync(repositoryPath, CancellationToken.None);
        var documentation = await systemUnderTests.GetDocumentationAsync(repositoryPath, CancellationToken.None);

        var facts = RepositoryFactsFactory.Compose(repositoryPath, activity, contributors, releases, codeHealth, documentation);
        var result = await RunHealthCheckAsync(facts, CancellationToken.None);

        // Assert
        activity.TotalCount.Should().Be(20);
        contributors.Should().NotBeEmpty();
        releases.Should().HaveCount(2);
        codeHealth.TodoCount.Should().BeGreaterThan(0);
        codeHealth.FixmeCount.Should().BeGreaterThan(0);
        documentation.HasReadme.Should().BeTrue();
        documentation.HasLicense.Should().BeTrue();
        result.Score.Should().BeInRange(0, 100);
        result.Categories.Should().HaveCount(6);
    }

    [Fact]
    public async Task ReadAndCheckAsync_WhenRealLargeThresholds_CompletesWithinTimeout()
    {
        // Arrange
        if (Environment.GetEnvironmentVariable(LargeRepositoryTestsEnvironmentVariable) != "1")
        {
            _output.WriteLine($"Тест пропущен. Задайте {LargeRepositoryTestsEnvironmentVariable}=1, чтобы запустить проверку реальных порогов.");
            return;
        }

        var files = ReadIntegerEnvironmentVariable(FilesEnvironmentVariable, 10000);
        var commits = ReadIntegerEnvironmentVariable(CommitsEnvironmentVariable, 20000);

        using var cancellationTokenSource = new CancellationTokenSource(GatedTimeout);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var repositoryPath = CreateLargeRepository(files, commits, authors: 5);
        var activity = await systemUnderTests.GetCommitActivityAsync(repositoryPath, cancellationTokenSource.Token);
        var contributors = await systemUnderTests.GetContributorsAsync(repositoryPath, cancellationTokenSource.Token);
        var releases = await systemUnderTests.GetReleasesAsync(repositoryPath, cancellationTokenSource.Token);
        var codeHealth = await systemUnderTests.GetCodeHealthAsync(repositoryPath, cancellationTokenSource.Token);
        var documentation = await systemUnderTests.GetDocumentationAsync(repositoryPath, cancellationTokenSource.Token);

        var facts = RepositoryFactsFactory.Compose(repositoryPath, activity, contributors, releases, codeHealth, documentation);
        var result = await RunHealthCheckAsync(facts, cancellationTokenSource.Token);

        stopwatch.Stop();

        // Assert
        activity.TotalCount.Should().Be(commits);
        result.Score.Should().BeInRange(0, 100);
        result.Categories.Should().HaveCount(6);
        stopwatch.Elapsed.Should().BeLessThan(GatedTimeout);
        _output.WriteLine($"Крупный репозиторий: файлов {files}, коммитов {commits}, мягкий лимит {GatedTimeout}, проанализирован за {stopwatch.Elapsed}.");
    }

    private string CreateLargeRepository(int files, int commits, int authors)
    {
        var repositoryPath = Path.Join(_tempFileSystem.Path, "large repository with spaces");
        TestRepositoryFactory.Create(
            repositoryPath,
            "create-large-repository.ps1",
            "-Files", files.ToString(),
            "-Commits", commits.ToString(),
            "-Authors", authors.ToString());

        return repositoryPath;
    }

    private async Task<HealthCheckResult> RunHealthCheckAsync(RepositoryFacts facts, CancellationToken cancellationToken)
    {
        var options = Options.Create(_healthCheckOptions);
        var timeProvider = new FixedTimeProvider(FixedNow);
        var normalizer = new MetricNormalizer(options);
        var categoryScoreCalculator = new CategoryScoreCalculator(options, normalizer, timeProvider);
        var healthScoreCalculator = new HealthScoreCalculator(options);
        var recommendationGenerator = new RecommendationGenerator(options);
        var engine = new HealthCheckEngine(
            options,
            categoryScoreCalculator,
            healthScoreCalculator,
            recommendationGenerator,
            timeProvider);

        return await engine.CheckAsync(facts, cancellationToken);
    }

    private static int ReadIntegerEnvironmentVariable(string name, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
