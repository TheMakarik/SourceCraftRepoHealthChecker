namespace SourceCraftRepoHealthChecker.infrastructure.Options;

public sealed class SourceCraftServiceOptions
{
    public required string BaseUrl { get; init; }
    public required int TimeoutSeconds { get; init; }
    public required int PageSize { get; init; }
    public string? InternalToken { get; init; }
}
