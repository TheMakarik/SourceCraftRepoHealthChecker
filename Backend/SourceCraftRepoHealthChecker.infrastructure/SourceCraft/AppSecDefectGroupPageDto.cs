namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record AppSecDefectGroupPageDto
{
    public IReadOnlyCollection<AppSecDefectGroupDto>? Data { get; init; }
    public string? NextPageToken { get; init; }
    public int TotalSize { get; init; }
}
