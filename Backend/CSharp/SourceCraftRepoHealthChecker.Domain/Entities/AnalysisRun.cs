using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Domain.Entities;

public sealed class AnalysisRun
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid? UserId { get; set; }
    public int Score { get; set; }
    public AnalysisStatus Status { get; set; }
    public DataStatus DataStatus { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public ICollection<CategoryScore> CategoryScores { get; set; } = [];
    public ICollection<Recommendation> Recommendations { get; set; } = [];
}
