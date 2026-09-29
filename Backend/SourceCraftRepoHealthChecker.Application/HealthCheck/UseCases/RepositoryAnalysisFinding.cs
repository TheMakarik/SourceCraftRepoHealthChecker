namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record RepositoryAnalysisFinding(
    string Kind,
    string Severity,
    string Status,
    string Title,
    string? Package,
    string? FilePath,
    double? CvssScore,
    string? ExternalId = null,
    int? FileLine = null,
    string? CommitSha = null);
