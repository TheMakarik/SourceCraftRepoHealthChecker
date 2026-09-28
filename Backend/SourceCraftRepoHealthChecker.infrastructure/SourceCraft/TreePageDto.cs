namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record TreePageDto
{
    public IReadOnlyCollection<TreeEntryDto>? Trees { get; init; }
    public string? NextPageToken { get; init; }
}
