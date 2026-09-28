namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed record AppSecDefectGroupDto
{
    public string? Uuid { get; init; }
    public AppSecDefectGroupId? PublicId { get; init; }
    public string? RuleName { get; init; }
    public string? RuleId { get; init; }
    public string? CodeBlock { get; init; }
    public string? FileName { get; init; }
    public string? LatestCommit { get; init; }
    public string? GitRepo { get; init; }
    public int Status { get; init; }
    public int Severity { get; init; }
    public double CvssScore { get; init; }
    public string? Engine { get; init; }
    public int EngineType { get; init; }
    public int FindingsCount { get; init; }
    public int StartLine { get; init; }
    public int EndLine { get; init; }
}
