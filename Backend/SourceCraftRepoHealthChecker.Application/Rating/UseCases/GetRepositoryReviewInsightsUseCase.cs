using SourceCraftRepoHealthChecker.Application.Rating.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

public sealed class GetRepositoryReviewInsightsUseCase(ISourceCraftCollaborationSource collaborationSource) : IGetRepositoryReviewInsightsUseCase
{
    public async Task<SourceCraftResult<RepositoryReviewInsights>> GetAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var result = await collaborationSource.GetMergeRequestsAsync(repositoryId, cancellationToken);
        if (result.Status != DataStatus.Available || result.Data is null)
            return new SourceCraftResult<RepositoryReviewInsights>(result.Status, null, result.Reason, result.IsPartial);

        return new SourceCraftResult<RepositoryReviewInsights>(DataStatus.Available, BuildInsights(result.Data), null, result.IsPartial);
    }

    private static RepositoryReviewInsights BuildInsights(IReadOnlyCollection<MergeRequestInfo> mergeRequests)
    {
        var evaluated = mergeRequests.Where(x => x.FirstResponseEvaluated).ToArray();
        var reviewed = evaluated.Where(x => x.ReviewCommentsCount > 0).ToArray();
        var responseDays = evaluated
            .Where(x => x.FirstResponseAt is not null)
            .Select(x => Math.Max(0, (x.FirstResponseAt!.Value - x.CreatedAt).TotalDays))
            .OrderBy(x => x)
            .ToArray();

        var share = evaluated.Length == 0 ? 0 : (double)reviewed.Length / evaluated.Length;
        return new RepositoryReviewInsights(
            mergeRequests.Count,
            evaluated.Length,
            reviewed.Length,
            evaluated.Length - reviewed.Length,
            Average(responseDays),
            Median(responseDays),
            Math.Round(share, 4),
            BuildReviewerLoad(evaluated));
    }

    private static IReadOnlyCollection<AuthorReviewLoad> BuildReviewerLoad(IReadOnlyCollection<MergeRequestInfo> evaluated) =>
        evaluated
            .GroupBy(x => x.AuthorLogin, StringComparer.Ordinal)
            .Select(group => new
            {
                AuthorLogin = group.Key,
                MergeRequestCount = group.Count(),
                ReviewedCount = group.Count(x => x.ReviewCommentsCount > 0),
                ResponseDays = group
                    .Where(x => x.FirstResponseAt is not null)
                    .Select(x => Math.Max(0, (x.FirstResponseAt!.Value - x.CreatedAt).TotalDays))
                    .ToArray()
            })
            .Select(item => new AuthorReviewLoad(
                item.AuthorLogin,
                item.MergeRequestCount,
                item.ReviewedCount,
                item.MergeRequestCount - item.ReviewedCount,
                Average(item.ResponseDays)))
            .OrderByDescending(item => item.MergeRequestCount)
            .ThenBy(item => item.AuthorLogin, StringComparer.Ordinal)
            .ToArray();

    private static double? Average(IReadOnlyCollection<double> values) =>
        values.Count == 0 ? null : Math.Round(values.Average(), 2);

    private static double? Median(IReadOnlyList<double> sortedValues)
    {
        if (sortedValues.Count == 0)
            return null;

        var middle = sortedValues.Count / 2;
        return sortedValues.Count % 2 == 1
            ? Math.Round(sortedValues[middle], 2)
            : Math.Round((sortedValues[middle - 1] + sortedValues[middle]) / 2, 2);
    }
}
