namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record RepositoryHistoryPoint(DateTimeOffset AnalyzedAt, int? Score);
