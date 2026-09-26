using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Domain.Entities;

public sealed class MetricScore
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public MetricCode Code { get; set; }
    public double RawValue { get; set; }
    public double NormalizedScore { get; set; }
    public double Weight { get; set; }
    public DataStatus DataStatus { get; set; }
}
