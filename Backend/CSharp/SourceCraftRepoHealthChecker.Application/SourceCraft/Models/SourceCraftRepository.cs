namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record SourceCraftRepository(
    string Id,
    string Name,
    string FullName,
    string Url,
    string Language,
    int LikesCount,
    DateTimeOffset LastActivityAt,
    bool IsPrivate,
    string DefaultBranch);
