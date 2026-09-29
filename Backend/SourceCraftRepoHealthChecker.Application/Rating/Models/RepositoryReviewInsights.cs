namespace SourceCraftRepoHealthChecker.Application.Rating.Models;

public sealed record RepositoryReviewInsights(
    int TotalMergeRequests,
    int EvaluatedMergeRequests,
    int ReviewedMergeRequests,
    int MergeRequestsWithoutReview,
    double? AverageTimeToFirstReviewDays,
    double? MedianTimeToFirstReviewDays,
    double ShareOfCommentedMergeRequests,
    IReadOnlyCollection<AuthorReviewLoad> ReviewerLoad);
