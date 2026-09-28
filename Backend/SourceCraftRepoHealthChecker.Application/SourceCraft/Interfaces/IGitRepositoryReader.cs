using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface IGitRepositoryReader
{
    public Task<CommitActivity> GetCommitActivityAsync(string repositoryPath, CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<Contributor>> GetContributorsAsync(string repositoryPath, CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<ReleaseInfo>> GetReleasesAsync(string repositoryPath, CancellationToken cancellationToken);

    public Task<CodeHealthReport> GetCodeHealthAsync(string repositoryPath, CancellationToken cancellationToken);

    public Task<DocumentationReport> GetDocumentationAsync(string repositoryPath, CancellationToken cancellationToken);
}
