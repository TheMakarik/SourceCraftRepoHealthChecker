namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed class RepositoryNotFoundException(string message) : Exception(message);
