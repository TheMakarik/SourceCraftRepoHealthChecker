namespace SourceCraftRepoHealthChecker.Application.Options;

public sealed class UserAiOptions
{
    public required int MaxBaseUrlLength { get; init; }
    public required int MaxModelLength { get; init; }
    public required int MaxTokenLength { get; init; }
}
