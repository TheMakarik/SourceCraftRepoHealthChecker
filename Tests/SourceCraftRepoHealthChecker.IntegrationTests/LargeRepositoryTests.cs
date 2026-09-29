using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Options;
using SourceCraftRepoHealthChecker.infrastructure.SourceCraft;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace SourceCraftRepoHealthChecker.IntegrationTests;

public sealed class LargeRepositoryTests : IDisposable
{
    private const string LargeRepositoryPathEnvironmentVariable = "SRHC_LARGE_REPO_PATH";
    private const string LegacyLargeRepositoryPathEnvironmentVariable = "LARGE_REPO_PATH";

    private const int LargeRepositoryFileCountThreshold = 10_000;
    private const int LargeRepositoryCommitCountThreshold = 20_000;
    private const long LargeRepositoryMegabyteThreshold = 500;

    private static readonly DateTimeOffset FixedNow = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan GatedTimeout = TimeSpan.FromMinutes(60);

    private readonly TempFileSystem _tempFileSystem = new();
    private readonly HealthCheckOptions _healthCheckOptions = HealthCheckOptionsLoader.Load();
    private readonly RepositoryBrowsingOptions _browsingOptions = LoadBrowsingOptions();
    private readonly LocalGitRepositoryReader _systemUnderTests;
    private readonly ITestOutputHelper _output;

    public LargeRepositoryTests(ITestOutputHelper output)
    {
        _output = output;
        _systemUnderTests = new LocalGitRepositoryReader(TimeProvider.System, Options.Create(_browsingOptions));
    }

    public void Dispose() => _tempFileSystem.Dispose();

    [Fact]
    public async Task ReadAndCheckAsync_WhenModeratelyLargeRepository_CompletesAndProducesScore()
    {
        // Arrange
        var repositoryPath = CreateLargeRepository(files: 2000, commits: 20, authors: 5);

        // Act
        var activity = await _systemUnderTests.GetCommitActivityAsync(repositoryPath, CancellationToken.None);
        var contributors = await _systemUnderTests.GetContributorsAsync(repositoryPath, CancellationToken.None);
        var releases = await _systemUnderTests.GetReleasesAsync(repositoryPath, CancellationToken.None);
        var codeHealth = await _systemUnderTests.GetCodeHealthAsync(repositoryPath, CancellationToken.None);
        var documentation = await _systemUnderTests.GetDocumentationAsync(repositoryPath, CancellationToken.None);

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

    // Runs a full end-to-end analysis (git history, code health, documentation, folders and the
    // health-check engine) on a large working copy supplied by the environment. The test is skipped
    // by default and only runs when the environment variable points to an existing working copy of a
    // repository that meets at least one large-repository criterion.
    //
    // How to run:
    //   SRHC_LARGE_REPO_PATH=/path/to/large/working-copy \
    //   dotnet test Tests/SourceCraftRepoHealthChecker.IntegrationTests \
    //     --filter "FullyQualifiedName~LargeRepositoryTests"
    //
    // The legacy variable name LARGE_REPO_PATH is also accepted. To run every large-repository test
    // explicitly: --filter "Category=LargeRepository".
    //
    // Qualified repositories: >= 10 000 tracked files, or >= 20 000 commits, or >= 500 MB on disk.
    [Fact]
    [Trait("Category", "LargeRepository")]
    public async Task AnalyzeAsync_WhenRealLargeRepositoryConfigured_CompletesAndProducesScore()
    {
        // Arrange
        var repositoryPath = ResolveLargeRepositoryPath();
        if (repositoryPath is null)
        {
            _output.WriteLine(
                $"Пропущено: задайте {LargeRepositoryPathEnvironmentVariable} (или {LegacyLargeRepositoryPathEnvironmentVariable}) — путь к рабочей копии крупного репозитория.");
            return;
        }

        using var cancellationTokenSource = new CancellationTokenSource(GatedTimeout);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var activity = await _systemUnderTests.GetCommitActivityAsync(repositoryPath, cancellationTokenSource.Token);
        var contributors = await _systemUnderTests.GetContributorsAsync(repositoryPath, cancellationTokenSource.Token);
        var releases = await _systemUnderTests.GetReleasesAsync(repositoryPath, cancellationTokenSource.Token);
        var codeHealth = await _systemUnderTests.GetCodeHealthAsync(repositoryPath, cancellationTokenSource.Token);
        var documentation = await _systemUnderTests.GetDocumentationAsync(repositoryPath, cancellationTokenSource.Token);
        var folders = await Task.Run(
            () => GitWorkingCopyContentReader.ReadFolders(repositoryPath, _browsingOptions.MaxFileBytes, cancellationTokenSource.Token),
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

    private static string? ResolveLargeRepositoryPath()
    {
        var repositoryPath = Environment.GetEnvironmentVariable(LargeRepositoryPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(repositoryPath))
            repositoryPath = Environment.GetEnvironmentVariable(LegacyLargeRepositoryPathEnvironmentVariable);

        return !string.IsNullOrWhiteSpace(repositoryPath) && Directory.Exists(repositoryPath)
            ? repositoryPath
            : null;
    }

    private static RepositoryBrowsingOptions LoadBrowsingOptions()
    {
        var path = Path.Join(AppContext.BaseDirectory, "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty(nameof(RepositoryBrowsingOptions), out var section))
            throw new InvalidOperationException("Секция RepositoryBrowsingOptions отсутствует в appsettings.json.");

        var options = JsonSerializer.Deserialize<RepositoryBrowsingOptions>(
            section.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return options ?? throw new InvalidOperationException("Не удалось прочитать RepositoryBrowsingOptions из appsettings.json.");
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
