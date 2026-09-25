using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public static class RepositoryFactsFactory
{
    public static async Task<RepositoryFacts> ReadAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        IGitRepositoryReader reader = new LocalGitRepositoryReader();

        var activity = await reader.GetCommitActivityAsync(repositoryPath, cancellationToken);
        var contributors = await reader.GetContributorsAsync(repositoryPath, cancellationToken);
        var releases = await reader.GetReleasesAsync(repositoryPath, cancellationToken);
        var codeHealth = await reader.GetCodeHealthAsync(repositoryPath, cancellationToken);
        var documentation = await reader.GetDocumentationAsync(repositoryPath, cancellationToken);

        return Compose(repositoryPath, activity, contributors, releases, codeHealth, documentation);
    }

    public static RepositoryFacts Compose(
        string repositoryPath,
        CommitActivity activity,
        IReadOnlyCollection<Contributor> contributors,
        IReadOnlyCollection<ReleaseInfo> releases,
        CodeHealthReport codeHealth,
        DocumentationReport documentation)
    {
        var name = Path.GetFileName(repositoryPath);
        var repository = new SourceCraftRepository(
            "local",
            name,
            $"local/{name}",
            "https://sourcecraft.dev/local",
            "C#",
            0,
            activity.LastCommitAt ?? DateTimeOffset.UnixEpoch,
            false,
            "main");

        return new RepositoryFacts(
            repository,
            DataStatus.Available,
            activity,
            contributors,
            releases,
            DataStatus.Unavailable,
            [],
            [],
            DataStatus.Unavailable,
            [],
            DataStatus.Unavailable,
            [],
            DataStatus.Available,
            codeHealth,
            DataStatus.Available,
            documentation);
    }
}
