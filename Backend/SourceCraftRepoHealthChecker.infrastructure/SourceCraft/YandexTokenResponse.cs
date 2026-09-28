using System.Text.Json.Serialization;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal sealed record YandexTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}
