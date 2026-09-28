namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record UserProfileDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Username { get; init; }
}
