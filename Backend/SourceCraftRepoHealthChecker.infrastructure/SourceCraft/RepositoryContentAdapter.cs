using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Options;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class RepositoryContentAdapter(
    GitWorkingCopyProvider workingCopyProvider,
    IOptions<RepositoryBrowsingOptions> options) : IRepositoryContentSource
{
    public async Task<SourceCraftResult<RepositoryFileContent>> GetFileAsync(
        string repositoryId,
        string path,
        CancellationToken cancellationToken)
    {
        var clone = await workingCopyProvider.AcquireAsync(repositoryId, cancellationToken);
        var workingCopy = clone.Data;
        if (clone.Status != DataStatus.Available || workingCopy is null)
            return new SourceCraftResult<RepositoryFileContent>(clone.Status, null, clone.Reason);

        using (workingCopy)
        {
            var maxFileBytes = options.Value.MaxFileBytes;
            var content = await Task.Run(
                () => GitWorkingCopyContentReader.ReadFile(workingCopy.Path, path, maxFileBytes),
                cancellationToken);
            if (content is null)
                return SourceCraftFailure.NoData<RepositoryFileContent>("file not found");

            return new SourceCraftResult<RepositoryFileContent>(DataStatus.Available, content, null);
        }
    }

    public async Task<SourceCraftResult<IReadOnlyList<RepositoryFolderAnalysis>>> GetFoldersAsync(
        string repositoryId,
        CancellationToken cancellationToken)
    {
        var clone = await workingCopyProvider.AcquireAsync(repositoryId, cancellationToken);
        var workingCopy = clone.Data;
        if (clone.Status != DataStatus.Available || workingCopy is null)
            return new SourceCraftResult<IReadOnlyList<RepositoryFolderAnalysis>>(clone.Status, null, clone.Reason);

        using (workingCopy)
        {
            var maxFileBytes = options.Value.MaxFileBytes;
            var folders = await Task.Run(
                () => GitWorkingCopyContentReader.ReadFolders(workingCopy.Path, maxFileBytes, cancellationToken),
                cancellationToken);
            return new SourceCraftResult<IReadOnlyList<RepositoryFolderAnalysis>>(DataStatus.Available, folders, null);
        }
    }
}
