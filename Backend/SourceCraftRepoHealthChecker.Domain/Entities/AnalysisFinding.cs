using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Domain.Entities;

public sealed class AnalysisFinding
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public SecurityFindingKind Kind { get; set; }
    public SecuritySeverity Severity { get; set; }
    public SecurityFindingStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Package { get; set; }
    public string? FilePath { get; set; }
    public double? CvssScore { get; set; }
    public string? ExternalId { get; set; }
    public int? FileLine { get; set; }
    public string? CommitSha { get; set; }
}
