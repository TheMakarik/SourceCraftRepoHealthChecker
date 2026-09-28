using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Options;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftTreeAdapter(
    ISourceCraftApi api,
    IOptions<RepositoryBrowsingOptions> options) : ISourceCraftTreeSource
{
    public async Task<SourceCraftResult<RepositoryTree>> GetTreeAsync(
        string repositoryId,
        string path,
        bool recursive,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var entries = new List<RepositoryTreeEntry>();
            var truncated = false;
            string? pageToken = null;

            for (var page = 0; page < settings.TreeMaxPages; page++)
            {
                var response = await api.GetTreesAsync(
                    repositoryId,
                    null,
                    path,
                    recursive,
                    settings.TreePageSize,
                    pageToken,
                    cancellationToken);

                foreach (var entry in response.Trees ?? [])
                {
                    var name = entry.Name;
                    var entryPath = entry.Path;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(entryPath))
                        continue;

                    entries.Add(new RepositoryTreeEntry(name, entryPath, entry.Type ?? "file"));
                }

                pageToken = response.NextPageToken;
                if (string.IsNullOrEmpty(pageToken))
                    break;
                if (page == settings.TreeMaxPages - 1)
                    truncated = true;
            }

            return new SourceCraftResult<RepositoryTree>(DataStatus.Available, new RepositoryTree(entries, truncated), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return SourceCraftFailure.Unavailable<RepositoryTree>(exception);
        }
    }
}
