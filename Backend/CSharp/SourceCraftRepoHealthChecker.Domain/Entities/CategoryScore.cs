using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Domain.Entities;

public sealed class CategoryScore
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public ScoreCategory Category { get; set; }
    public int Score { get; set; }
    public DataStatus DataStatus { get; set; }
}
