using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftCollaborationAdapter(
    ISourceCraftApi api,
    IOptions<SourceCraftServiceOptions> options) : ISourceCraftCollaborationSource
{
    public async Task<SourceCraftResult<IReadOnlyCollection<IssueInfo>>> GetIssuesAsync(string repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var issues = await SourceCraftPagination.CollectAsync(
                settings.MaxItems,
                settings.MaxPages,
                (pageToken, token) => FetchIssuesAsync(repositoryId, pageToken, token),
                cancellationToken);

            var results = issues.Select(MapIssue).ToArray();
            var lookupCount = Math.Min(results.Length, settings.MaxResponseLookups);
            MarkUnevaluatedResponses(results, lookupCount);
            var error = await FetchIssueFirstResponsesAsync(issues, results, lookupCount, cancellationToken);
            if (error is not null)
                return SourceCraftFailure.Unavailable<IReadOnlyCollection<IssueInfo>>(error);

            var isPartial = issues.Truncated || results.Length > settings.MaxResponseLookups;
            return new SourceCraftResult<IReadOnlyCollection<IssueInfo>>(DataStatus.Available, results, null, isPartial);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return SourceCraftFailure.Unavailable<IReadOnlyCollection<IssueInfo>>(exception);
        }
    }

    public async Task<SourceCraftResult<IReadOnlyCollection<MergeRequestInfo>>> GetMergeRequestsAsync(string repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var pullRequests = await SourceCraftPagination.CollectAsync(
                settings.MaxItems,
                settings.MaxPages,
                (pageToken, token) => FetchPullRequestsAsync(repositoryId, pageToken, token),
                cancellationToken);

            var results = pullRequests.Select(MapPullRequest).ToArray();
            var lookupCount = Math.Min(results.Length, settings.MaxResponseLookups);
            MarkUnevaluatedResponses(results, lookupCount);
            var error = await FetchPullRequestResponsesAsync(pullRequests, results, lookupCount, cancellationToken);
            if (error is not null)
                return SourceCraftFailure.Unavailable<IReadOnlyCollection<MergeRequestInfo>>(error);

            var isPartial = pullRequests.Truncated || results.Length > settings.MaxResponseLookups;
            return new SourceCraftResult<IReadOnlyCollection<MergeRequestInfo>>(DataStatus.Available, results, null, isPartial);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return SourceCraftFailure.Unavailable<IReadOnlyCollection<MergeRequestInfo>>(exception);
        }
    }

    private static void MarkUnevaluatedResponses(IList<IssueInfo> results, int lookupCount)
    {
        for (var index = lookupCount; index < results.Count; index++)
            results[index] = results[index] with { FirstResponseEvaluated = false };
    }

    private static void MarkUnevaluatedResponses(IList<MergeRequestInfo> results, int lookupCount)
    {
        for (var index = lookupCount; index < results.Count; index++)
            results[index] = results[index] with { FirstResponseEvaluated = false };
    }

    private async Task<Exception?> FetchIssueFirstResponsesAsync(
        IReadOnlyList<IssueDto> issues,
        IssueInfo[] results,
        int lookupCount,
        CancellationToken cancellationToken)
    {
        if (lookupCount <= 0)
            return null;

        var settings = options.Value;
        ConcurrentQueue<Exception> errors = new();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, lookupCount),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, settings.Concurrency),
                CancellationToken = cancellationToken
            },
            async (index, token) =>
            {
                try
                {
                    var comments = await SourceCraftPagination.CollectAsync(
                        settings.IssueCommentLimit,
                        settings.MaxPages,
                        (pageToken, innerToken) => FetchIssueCommentsAsync(issues[index].Id ?? string.Empty, pageToken, innerToken),
                        token);

                    foreach (var comment in comments)
                    {
                        if (!string.Equals(comment.Author?.Id, issues[index].Author?.Id, StringComparison.Ordinal))
                        {
                            results[index] = results[index] with { FirstResponseAt = comment.CreatedAt };
                            break;
                        }
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    errors.Enqueue(exception);
                }
            });

        return errors.TryDequeue(out var firstError) ? firstError : null;
    }

    private async Task<Exception?> FetchPullRequestResponsesAsync(
        IReadOnlyList<PullRequestDto> pullRequests,
        MergeRequestInfo[] results,
        int lookupCount,
        CancellationToken cancellationToken)
    {
        if (lookupCount <= 0)
            return null;

        var settings = options.Value;
        ConcurrentQueue<Exception> errors = new();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, lookupCount),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, settings.Concurrency),
                CancellationToken = cancellationToken
            },
            async (index, token) =>
            {
                try
                {
                    var comments = await SourceCraftPagination.CollectAsync(
                        0,
                        settings.MaxPages,
                        (pageToken, innerToken) => FetchPullRequestCommentsAsync(pullRequests[index].Id ?? string.Empty, pageToken, innerToken),
                        token);

                    foreach (var comment in comments)
                    {
                        if (comment.IsDeleted || !comment.IsPublished)
                            continue;
                        if (string.Equals(comment.Author?.Id, pullRequests[index].Author?.Id, StringComparison.Ordinal))
                            continue;

                        results[index] = results[index] with { ReviewCommentsCount = results[index].ReviewCommentsCount + 1 };
                        if (results[index].FirstResponseAt is null)
                            results[index] = results[index] with { FirstResponseAt = comment.CreatedAt };
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    errors.Enqueue(exception);
                }
            });

        return errors.TryDequeue(out var firstError) ? firstError : null;
    }

    private async Task<(IReadOnlyCollection<IssueDto> Items, string? NextPageToken)> FetchIssuesAsync(
        string repositoryId,
        string? pageToken,
        CancellationToken cancellationToken)
    {
        var page = await api.GetIssuesAsync(repositoryId, "-created_at", options.Value.PageSize, pageToken, cancellationToken);
        return (page.Issues ?? [], page.NextPageToken);
    }

    private async Task<(IReadOnlyCollection<IssueCommentDto> Items, string? NextPageToken)> FetchIssueCommentsAsync(
        string issueId,
        string? pageToken,
        CancellationToken cancellationToken)
    {
        var page = await api.GetIssueCommentsAsync(issueId, "created_at", options.Value.PageSize, pageToken, cancellationToken);
        return (page.IssueComments ?? [], page.NextPageToken);
    }

    private async Task<(IReadOnlyCollection<PullRequestDto> Items, string? NextPageToken)> FetchPullRequestsAsync(
        string repositoryId,
        string? pageToken,
        CancellationToken cancellationToken)
    {
        var page = await api.GetPullRequestsAsync(repositoryId, "-created_at", options.Value.PageSize, pageToken, cancellationToken);
        return (page.PullRequests ?? [], page.NextPageToken);
    }

    private async Task<(IReadOnlyCollection<PullRequestCommentDto> Items, string? NextPageToken)> FetchPullRequestCommentsAsync(
        string pullRequestId,
        string? pageToken,
        CancellationToken cancellationToken)
    {
        var page = await api.GetPullRequestCommentsAsync(pullRequestId, "created_at", options.Value.PageSize, pageToken, cancellationToken);
        return (page.PullRequestComments ?? [], page.NextPageToken);
    }

    private static IssueInfo MapIssue(IssueDto issue)
    {
        var state = IssueState.Open;
        DateTimeOffset? closedAt = null;
        if (issue.Status?.StatusType is "completed" or "cancelled")
        {
            state = IssueState.Closed;
            closedAt = issue.CompletedAt ?? issue.UpdatedAt;
        }

        return new IssueInfo(
            issue.Id ?? string.Empty,
            issue.Title ?? string.Empty,
            state,
            issue.Author?.Slug ?? string.Empty,
            issue.CreatedAt,
            closedAt,
            null,
            issue.UpdatedAt);
    }

    private static MergeRequestInfo MapPullRequest(PullRequestDto pullRequest)
    {
        var state = MergeRequestState.Open;
        DateTimeOffset? mergedAt = null;
        DateTimeOffset? closedAt = null;
        switch (pullRequest.Status)
        {
            case "merged":
                state = MergeRequestState.Merged;
                mergedAt = pullRequest.UpdatedAt;
                closedAt = pullRequest.UpdatedAt;
                break;
            case "discarded":
                state = MergeRequestState.Closed;
                closedAt = pullRequest.UpdatedAt;
                break;
        }

        return new MergeRequestInfo(
            pullRequest.Id ?? string.Empty,
            pullRequest.Title ?? string.Empty,
            state,
            pullRequest.Author?.Slug ?? string.Empty,
            pullRequest.CreatedAt,
            mergedAt,
            closedAt,
            null,
            0);
    }
}
