using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Domain.Entities;

public sealed class UserAi
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AiProviders AiProvider { get; set; }
    public string? AiBaseUrl { get; set; }
    public string AiModel { get; set; } = string.Empty;
    public string AiToken { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
