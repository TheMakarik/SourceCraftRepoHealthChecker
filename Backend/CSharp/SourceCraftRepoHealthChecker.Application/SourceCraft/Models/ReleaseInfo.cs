namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record ReleaseInfo(
    string Name,
    string Tag,
    DateTimeOffset PublishedAt);
