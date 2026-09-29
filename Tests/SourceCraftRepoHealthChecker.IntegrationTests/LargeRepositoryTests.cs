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
    private const string LargeRepositoryPathEnvironmentVariable = "LARGE_REPO_PATH";

    private const int LargeRepositoryFileCountThreshold = 10_000;
    private const int LargeRepositoryCommitCountThreshold = 20_000;
    private const long LargeRepositoryMegabyteThreshold = 500;
    private const long AnalysisMaxFileBytes = 204800;

    private static readonly DateTimeOffset FixedNow = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan GatedTimeout = TimeSpan.FromMinutes(60);

    private readonly TempFileSystem _tempFileSystem = new();
    private readonly HealthCheckOptions _healthCheckOptions = HealthCheckOptionsLoader.Load();
    private readonly LocalGitRepositoryReader systemUnderTests = new(TimeProvider.System);
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

    // Runs a full analysis (git history, code health, documentation, folders and the health-check
    // engine) on a large working copy supplied by the environment. The test is skipped by default
    // and only verifies that analysis completes and produces a score for a repository that meets at
    // least one large-repository criterion.
    //
    // How to run:
    //   LARGE_REPO_PATH=/path/to/large/working-copy \
    //   dotnet test Tests/SourceCraftRepoHealthChecker.IntegrationTests \
    //     --filter "FullyQualifiedName~LargeRepositoryTests"
    //
    // Qualified repositories: >= 10 000 tracked files, or >= 20 000 commits, or >= 500 MB on disk.
    [Fact]
    public async Task AnalyzeAsync_WhenRealLargeRepositoryConfigured_CompletesAndProducesScore()
    {
        // Arrange
        var repositoryPath = Environment.GetEnvironmentVariable(LargeRepositoryPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            _output.WriteLine($"Set {LargeRepositoryPathEnvironmentVariable} to a large repository working copy to run this test.");
            return;
        }

        using var cancellationTokenSource = new CancellationTokenSource(GatedTimeout);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var activity = await systemUnderTests.GetCommitActivityAsync(repositoryPath, cancellationTokenSource.Token);
        var contributors = await systemUnderTests.GetContributorsAsync(repositoryPath, cancellationTokenSource.Token);
        var releases = await systemUnderTests.GetReleasesAsync(repositoryPath, cancellationTokenSource.Token);
        var codeHealth = await systemUnderTests.GetCodeHealthAsync(repositoryPath, cancellationTokenSource.Token);
        var documentation = await systemUnderTests.GetDocumentationAsync(repositoryPath, cancellationTokenSource.Token);
        var folders = await Task.Run(
            () => GitWorkingCopyContentReader.ReadFolders(repositoryPath, AnalysisMaxFileBytes, cancellationTokenSource.Token),
            cancellationTokenSource.Token);

        var facts = RepositoryFactsFactory.Compose(repositoryPath, activity, contributors, releases, codeHealth, documentation);
        var result = await RunHealthCheckAsync(facts, cancellationTokenSource.Token);

        stopwatch.Stop();

        // Assert
        var trackedFiles = folders.Sum(folder => folder.Files);
        var repositoryMegabytes = GetDirectorySizeMegabytes(repositoryPath);
        var isLargeRepository =
            trackedFiles >= LargeRepositoryFileCountThreshold
            || activity.TotalCount >= LargeRepositoryCommitCountThreshold
            || repositoryMegabytes >= LargeRepositoryMegabyteThreshold;

        isLargeRepository.Should().BeTrue(
            $"the repository must meet one large-repository criterion: files >= {LargeRepositoryFileCountThreshold}, commits >= {LargeRepositoryCommitCountThreshold} or size >= {LargeRepositoryMegabyteThreshold} MB");

        activity.TotalCount.Should().BeGreaterThan(0);
        contributors.Should().NotBeEmpty();
        folders.Should().NotBeEmpty();
        result.Score.Should().BeInRange(0, 100);
        result.Categories.Should().HaveCount(6);
        stopwatch.Elapsed.Should().BeLessThan(GatedTimeout);
        _output.WriteLine(
            $"Крупный репозиторий: файлов {trackedFiles}, коммитов {activity.TotalCount}, размер {repositoryMegabytes} МБ, проанализирован за {stopwatch.Elapsed}.");
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

    private static long GetDirectorySizeMegabytes(string directory)
    {
        var totalBytes = 0L;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            try
            {
                totalBytes += new FileInfo(file).Length;
            }
            catch (IOException)
            {
            }
        }

        return totalBytes / (1024 * 1024);
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
}
