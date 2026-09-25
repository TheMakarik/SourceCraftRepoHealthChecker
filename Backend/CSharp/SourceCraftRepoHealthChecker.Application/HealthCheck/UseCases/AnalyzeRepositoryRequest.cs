namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record AnalyzeRepositoryRequest(
    string RepositoryId,
    Guid? UserId = null);
