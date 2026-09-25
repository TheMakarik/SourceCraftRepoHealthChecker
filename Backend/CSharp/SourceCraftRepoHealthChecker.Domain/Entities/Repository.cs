namespace SourceCraftRepoHealthChecker.Domain.Entities;

public sealed class Repository
{
    public Guid Id { get; set; }
    public string SourceCraftId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public int LikesCount { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AnalyzedAt { get; set; }

    public ICollection<AnalysisRun> AnalysisRuns { get; set; } = [];
}
