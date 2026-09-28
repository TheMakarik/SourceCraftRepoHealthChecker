namespace SourceCraftRepoHealthChecker.infrastructure.Options;

public sealed class YandexIdOptions
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string RedirectUri { get; init; }
    public required string Scope { get; init; }
    public required string OAuthUrl { get; init; }
    public required string LoginUrl { get; init; }
    public required int TimeoutSeconds { get; init; }
}
