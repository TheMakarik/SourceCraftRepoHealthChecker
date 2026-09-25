namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record IssueInfo(
    string Id,
    string Title,
    IssueState State,
    string AuthorLogin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? UpdatedAt = null);
