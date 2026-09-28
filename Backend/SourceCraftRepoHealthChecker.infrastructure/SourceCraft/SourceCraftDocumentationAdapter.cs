using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftDocumentationAdapter(
    GitWorkingCopyProvider workingCopyProvider,
    IGitRepositoryReader gitRepositoryReader) : ISourceCraftDocumentationSource
{
    public async Task<SourceCraftResult<DocumentationReport>> GetDocumentationAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var clone = await workingCopyProvider.AcquireAsync(repositoryId, cancellationToken);
        var workingCopy = clone.Data;
        if (clone.Status != DataStatus.Available || workingCopy is null)
            return new SourceCraftResult<DocumentationReport>(clone.Status, null, clone.Reason);

        using (workingCopy)
        {
            var report = await gitRepositoryReader.GetDocumentationAsync(workingCopy.Path, cancellationToken);
            return new SourceCraftResult<DocumentationReport>(DataStatus.Available, report, null);
        }
    }
}
