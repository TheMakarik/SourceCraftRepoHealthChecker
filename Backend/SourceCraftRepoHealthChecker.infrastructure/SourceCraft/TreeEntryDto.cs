namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record TreeEntryDto
{
    public string? Name { get; init; }
    public string? Path { get; init; }
    public string? Type { get; init; }
}
