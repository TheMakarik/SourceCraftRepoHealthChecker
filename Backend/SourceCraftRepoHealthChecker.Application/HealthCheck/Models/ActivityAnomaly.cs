namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Models;

public sealed record ActivityAnomaly(
    ActivityAnomalyKind Kind,
    string AuthorLogin,
    int Count,
    DateTimeOffset? FirstAt,
    DateTimeOffset? LastAt,
    string Description);
