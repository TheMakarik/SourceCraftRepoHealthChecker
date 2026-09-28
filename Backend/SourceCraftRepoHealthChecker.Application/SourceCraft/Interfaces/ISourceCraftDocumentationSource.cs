using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftDocumentationSource
{
    public Task<SourceCraftResult<DocumentationReport>> GetDocumentationAsync(string repositoryId, CancellationToken cancellationToken);
}
