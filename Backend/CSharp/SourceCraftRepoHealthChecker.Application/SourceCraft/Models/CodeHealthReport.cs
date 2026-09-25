namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record CodeHealthReport(
    int TodoCount,
    int FixmeCount,
    int TotalCommentCount,
    TimeSpan? OldestCommentAge);
