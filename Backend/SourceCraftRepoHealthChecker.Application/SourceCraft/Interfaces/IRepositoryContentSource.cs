using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface IRepositoryContentSource
{
    public Task<SourceCraftResult<RepositoryFileContent>> GetFileAsync(
        string repositoryId,
        string path,
        CancellationToken cancellationToken);

    public Task<SourceCraftResult<IReadOnlyList<RepositoryFolderAnalysis>>> GetFoldersAsync(
        string repositoryId,
        CancellationToken cancellationToken);
}
