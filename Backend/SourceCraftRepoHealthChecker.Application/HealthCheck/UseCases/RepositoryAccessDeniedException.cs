namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed class RepositoryAccessDeniedException(string message) : Exception(message);
