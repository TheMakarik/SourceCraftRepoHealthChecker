namespace SourceCraftRepoHealthChecker.infrastructure.Options;

public sealed class AppSecOptions
{
    public required string BaseUrl { get; init; }
    public required int TimeoutSeconds { get; init; }
    public required int MaxRetries { get; init; }
    public required int PageSize { get; init; }
    public required int MaxPages { get; init; }
}
