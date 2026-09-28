namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record CommitActivity(
    int TotalCount,
    DateTimeOffset? FirstCommitAt,
    DateTimeOffset? LastCommitAt,
    IReadOnlyDictionary<DateOnly, int> CommitsByDay);
