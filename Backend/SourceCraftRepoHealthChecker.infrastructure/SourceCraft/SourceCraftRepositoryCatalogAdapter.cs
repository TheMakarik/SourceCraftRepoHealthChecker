using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftRepositoryCatalogAdapter(
    ISourceCraftApi api,
    IOptions<SourceCraftServiceOptions> options,
    ILogger<SourceCraftRepositoryCatalogAdapter> logger) : ISourceCraftRepositoryCatalog
{
    public async Task<SourceCraftResult<IReadOnlyCollection<SourceCraftRepository>>> GetOpenRepositoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var repositories = await SourceCraftPagination.CollectAsync(
                0,
                settings.MaxPages,
                (pageToken, token) => FetchRepositoriesAsync(pageToken, token),
                cancellationToken,
                logger);
            if (repositories.Count == 0)
                return SourceCraftFailure.NoData<IReadOnlyCollection<SourceCraftRepository>>("source returned no repositories");

            return new SourceCraftResult<IReadOnlyCollection<SourceCraftRepository>>(
                DataStatus.Available,
                repositories.Select(SourceCraftRepositoryMapper.Map).ToArray(),
                null,
                repositories.Truncated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return SourceCraftFailure.Unavailable<IReadOnlyCollection<SourceCraftRepository>>(exception);
        }
    }

    public async Task<SourceCraftResult<SourceCraftRepository>> GetRepositoryAsync(string repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            var repository = await api.GetRepositoryAsync(repositoryId, cancellationToken);
            return new SourceCraftResult<SourceCraftRepository>(
                DataStatus.Available,
                SourceCraftRepositoryMapper.Map(repository),
                null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return SourceCraftFailure.Unavailable<SourceCraftRepository>(exception);
        }
    }

    private async Task<(IReadOnlyCollection<RepositoryDto> Items, string? NextPageToken)> FetchRepositoriesAsync(
        string? pageToken,
        CancellationToken cancellationToken)
    {
        var page = await api.GetRepositoriesAsync(options.Value.PageSize, pageToken, null, cancellationToken);
        return (page.Repositories ?? [], page.NextPageToken);
    }
}
