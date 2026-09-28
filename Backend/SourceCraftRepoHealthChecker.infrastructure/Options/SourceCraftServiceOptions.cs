namespace SourceCraftRepoHealthChecker.infrastructure.Options;

public sealed class SourceCraftServiceOptions
{
    public required string BaseUrl { get; init; }
    public required string ServiceToken { get; init; }
    public required int TimeoutSeconds { get; init; }
    public required int PageSize { get; init; }
    public required int MaxItems { get; init; }
    public required int MaxResponseLookups { get; init; }
    public required int IssueCommentLimit { get; init; }
    public required int Concurrency { get; init; }
    public required int MaxPages { get; init; }
    public string? InternalToken { get; init; }
}
