namespace SourceCraftRepoHealthChecker.Application.Ownership.Options;

public sealed class OwnershipOptions
{
    public int CoverageThresholdPercent { get; init; } = 50;
    public int TopDirectoriesCount { get; init; } = 3;
}
