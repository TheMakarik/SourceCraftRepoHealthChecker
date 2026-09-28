namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record OrganizationDto
{
    public string? Id { get; init; }
    public string? Slug { get; init; }
}
