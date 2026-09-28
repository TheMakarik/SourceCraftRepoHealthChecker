using System.Text.Json.Serialization;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

internal sealed record YandexUserInfoResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("login")]
    public string? Login { get; init; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("real_name")]
    public string? RealName { get; init; }

    [JsonPropertyName("default_email")]
    public string? DefaultEmail { get; init; }

    [JsonPropertyName("default_avatar_id")]
    public string? DefaultAvatarId { get; init; }

    [JsonPropertyName("is_avatar_empty")]
    public bool IsAvatarEmpty { get; init; }
}
