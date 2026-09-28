namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record MergeRequestInfo(
    string Id,
    string Title,
    MergeRequestState State,
    string AuthorLogin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? MergedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? FirstResponseAt,
    int ReviewCommentsCount);
