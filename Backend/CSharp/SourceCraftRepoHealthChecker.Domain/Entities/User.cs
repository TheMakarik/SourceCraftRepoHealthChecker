namespace SourceCraftRepoHealthChecker.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string YaId { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? SourceCraftToken { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public UserAi? Ai { get; set; }
    public ICollection<AnalysisRun> AnalysisRuns { get; set; } = [];
}
