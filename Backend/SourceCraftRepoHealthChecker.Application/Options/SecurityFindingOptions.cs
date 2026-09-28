namespace SourceCraftRepoHealthChecker.Application.Options;

public sealed class SecurityFindingOptions
{
    public required int MaxTitleLength { get; init; }
    public required int MaxPackageLength { get; init; }
    public required int MaxFilePathLength { get; init; }
}
