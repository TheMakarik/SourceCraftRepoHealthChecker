namespace SourceCraftRepoHealthChecker.Application.Rating.Models;

public sealed record AuthorReviewLoad(
    string AuthorLogin,
    int MergeRequestCount,
    int ReviewedMergeRequestCount,
    int UnreviewedMergeRequestCount,
    double? AverageTimeToFirstReviewDays);
