namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record ReleasePageDto
{
    public IReadOnlyCollection<ReleaseDto>? Releases { get; init; }
    public string? NextPageToken { get; init; }
}
