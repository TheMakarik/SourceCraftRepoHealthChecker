namespace SourceCraftRepoHealthChecker.Application.Options;

public sealed class RecommendationOptions
{
    public required int MaxTitleLength { get; init; }
    public required int MaxProblemLength { get; init; }
    public required int MaxWhyImportantLength { get; init; }
    public required int MaxEvidenceLength { get; init; }
    public required int MaxActionLength { get; init; }
    public required int MaxExpectedImpactLength { get; init; }
    public required int MaxSourceReferenceLength { get; init; }
}
