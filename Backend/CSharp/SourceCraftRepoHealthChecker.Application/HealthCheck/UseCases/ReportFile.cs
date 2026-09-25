namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public sealed record ReportFile(string ContentType, byte[] Content);
