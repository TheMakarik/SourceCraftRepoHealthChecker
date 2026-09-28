using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Domain.Entities;

public sealed class Recommendation
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public RecommendationPriority Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public string WhyImportant { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ExpectedImpact { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
}
