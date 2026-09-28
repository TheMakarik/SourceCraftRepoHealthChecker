namespace SourceCraftRepoHealthChecker.Application.Analysis;

public sealed record AnalysisStatusEvent(
    string RepositoryId,
    string Status,
    int? Score,
    DateTimeOffset UpdatedAt);
