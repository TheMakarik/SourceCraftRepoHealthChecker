using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;

public interface IReportPdfRenderer
{
    public byte[] Render(RepositoryAnalysis analysis);
}
