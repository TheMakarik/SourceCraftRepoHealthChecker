namespace SourceCraftRepoHealthChecker.Application.Rating.UseCases;

internal sealed class LeaderboardRow
{
    public string SourceCraftId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int LikesCount { get; set; }
    public string Language { get; set; } = string.Empty;
    public DateTimeOffset LastActivityAt { get; set; }
    public int? Score { get; set; }
    public DateTimeOffset? AnalyzedAt { get; set; }
    public int? PreviousScore { get; set; }
    public bool HasCi { get; set; }
}
