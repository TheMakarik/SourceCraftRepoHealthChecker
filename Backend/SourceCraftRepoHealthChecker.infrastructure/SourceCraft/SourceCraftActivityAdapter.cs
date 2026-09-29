using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftActivityAdapter(
    GitWorkingCopyProvider workingCopyProvider,
    IGitRepositoryReader gitRepositoryReader,
    ISourceCraftApi api,
    IOptions<SourceCraftServiceOptions> options) : ISourceCraftActivitySource
{
    public async Task<SourceCraftResult<CommitActivity>> GetCommitActivityAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var clone = await workingCopyProvider.AcquireAsync(repositoryId, cancellationToken);
        var workingCopy = clone.Data;
        if (clone.Status != DataStatus.Available || workingCopy is null)
            return new SourceCraftResult<CommitActivity>(clone.Status, null, clone.Reason);

        using (workingCopy)
        {
            var activity = await gitRepositoryReader.GetCommitActivityAsync(workingCopy.Path, cancellationToken);

            return new SourceCraftResult<CommitActivity>(DataStatus.Available, activity, null);
        }
    }

    public async Task<SourceCraftResult<IReadOnlyCollection<Contributor>>> GetContributorsAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var clone = await workingCopyProvider.AcquireAsync(repositoryId, cancellationToken);
        var workingCopy = clone.Data;
        if (clone.Status != DataStatus.Available || workingCopy is null)
            return new SourceCraftResult<IReadOnlyCollection<Contributor>>(clone.Status, null, clone.Reason);

        using (workingCopy)
        {
            var contributors = await gitRepositoryReader.GetContributorsAsync(workingCopy.Path, cancellationToken);

            return new SourceCraftResult<IReadOnlyCollection<Contributor>>(DataStatus.Available, contributors, null);
        }
    }

    public async Task<SourceCraftResult<IReadOnlyCollection<ReleaseInfo>>> GetReleasesAsync(string repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var releases = await SourceCraftPagination.CollectAsync(
                0,
                settings.MaxPages,
                (pageToken, token) => FetchReleasesAsync(repositoryId, pageToken, token),
                cancellationToken);

            var published = releases
                .Where(release => string.Equals(release.Status, "published", StringComparison.Ordinal))
                .Select(MapRelease)
                .ToArray();

            return new SourceCraftResult<IReadOnlyCollection<ReleaseInfo>>(DataStatus.Available, published, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return SourceCraftFailure.Unavailable<IReadOnlyCollection<ReleaseInfo>>(exception);
        }
    }

    private async Task<(IReadOnlyCollection<ReleaseDto> Items, string? NextPageToken)> FetchReleasesAsync(
        string repositoryId,
        string? pageToken,
        CancellationToken cancellationToken)
    {
        var page = await api.GetReleasesAsync(repositoryId, options.Value.PageSize, pageToken, cancellationToken);
        return (page.Releases ?? [], page.NextPageToken);
    }

    private static ReleaseInfo MapRelease(ReleaseDto release) =>
        new(release.Title ?? string.Empty, release.Tag ?? string.Empty, release.ReleasedAt ?? release.CreatedAt);
}
