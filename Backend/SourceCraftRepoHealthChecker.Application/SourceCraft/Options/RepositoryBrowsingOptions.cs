namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Options;

public sealed class RepositoryBrowsingOptions
{
    public required long MaxFileBytes { get; init; }
    public required int TreePageSize { get; init; }
    public required int TreeMaxPages { get; init; }
}
