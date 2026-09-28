namespace SourceCraftRepoHealthChecker.infrastructure.Options;

public sealed class CorsOptions
{
    public string[] AllowedOrigins { get; init; } = [];
}
