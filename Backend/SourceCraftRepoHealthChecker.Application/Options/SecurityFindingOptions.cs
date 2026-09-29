namespace SourceCraftRepoHealthChecker.Application.Options;

public sealed class SecurityFindingOptions
{
    public required int MaxTitleLength { get; init; }
    public required int MaxPackageLength { get; init; }
    public required int MaxFilePathLength { get; init; }
    public int MaxExternalIdLength { get; init; } = 256;
    public int MaxCommitShaLength { get; init; } = 64;
}
