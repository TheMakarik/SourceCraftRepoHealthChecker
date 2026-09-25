using FluentAssertions;
using SourceCraftRepoHealthChecker.infrastructure.SourceCraft;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

namespace SourceCraftRepoHealthChecker.IntegrationTests;

public sealed class GitRepositoryReaderTests : IDisposable
{
    private readonly TempFileSystem _tempFileSystem = new();
    private readonly string _repositoryPath;
    private readonly LocalGitRepositoryReader systemUnderTests = new();

    public GitRepositoryReaderTests()
    {
        _repositoryPath = Path.Join(_tempFileSystem.Path, "test-repository");
        TestRepositoryFactory.Create(_repositoryPath, "create-test-repository.ps1");
    }

    public void Dispose() => _tempFileSystem.Dispose();

    [Fact]
    public async Task GetCommitActivityAsync_WhenRepositoryHasCommits_ReturnsCountAndDateRange()
    {
        // Act
        var activity = await systemUnderTests.GetCommitActivityAsync(_repositoryPath, CancellationToken.None);

        // Assert
        activity.TotalCount.Should().Be(4);
        activity.FirstCommitAt.Should().NotBeNull();
        activity.LastCommitAt.Should().NotBeNull();
        activity.FirstCommitAt!.Value.Should().BeBefore(activity.LastCommitAt!.Value);
        activity.CommitsByDay.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetContributorsAsync_WhenRepositoryHasMultipleAuthors_ReturnsGroupedContributors()
    {
        // Act
        var contributors = await systemUnderTests.GetContributorsAsync(_repositoryPath, CancellationToken.None);

        // Assert
        contributors.Should().HaveCount(3);
        contributors.Should().ContainSingle(contributor => contributor.Login == "Alice Developer" && contributor.CommitsCount == 2);
        contributors.Should().ContainSingle(contributor => contributor.Login == "dependabot[bot]" && contributor.IsBot);
    }

    [Fact]
    public async Task GetReleasesAsync_WhenRepositoryHasTags_ReturnsAllReleases()
    {
        // Act
        var releases = await systemUnderTests.GetReleasesAsync(_repositoryPath, CancellationToken.None);

        // Assert
        releases.Should().HaveCount(2);
        releases.Select(release => release.Tag).Should().BeEquivalentTo(["v1.0.0", "v1.1.0"]);
        releases.Should().OnlyContain(release => release.PublishedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task GetCodeHealthAsync_WhenRepositoryHasMarkers_ReturnsMarkerCounts()
    {
        // Act
        var report = await systemUnderTests.GetCodeHealthAsync(_repositoryPath, CancellationToken.None);

        // Assert
        report.TodoCount.Should().Be(3);
        report.FixmeCount.Should().Be(2);
        report.TotalCommentCount.Should().BeGreaterThan(0);
        report.OldestCommentAge.Should().NotBeNull();
        report.OldestCommentAge!.Value.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task GetDocumentationAsync_WhenRepositoryHasDocs_DetectsEveryDocument()
    {
        // Act
        var report = await systemUnderTests.GetDocumentationAsync(_repositoryPath, CancellationToken.None);

        // Assert
        report.HasReadme.Should().BeTrue();
        report.HasLicense.Should().BeTrue();
        report.HasContributing.Should().BeTrue();
        report.HasCodeOwners.Should().BeTrue();
        report.HasLocalRunInstructions.Should().BeTrue();
        report.HasBuildAndTestInstructions.Should().BeTrue();
    }

    [Fact]
    public void TempFileSystem_AfterDispose_RemovesDirectory()
    {
        // Arrange
        var tempFileSystem = new TempFileSystem();
        var path = tempFileSystem.Path;
        Directory.Exists(path).Should().BeTrue();

        // Act
        tempFileSystem.Dispose();

        // Assert
        Directory.Exists(path).Should().BeFalse();
    }
}
