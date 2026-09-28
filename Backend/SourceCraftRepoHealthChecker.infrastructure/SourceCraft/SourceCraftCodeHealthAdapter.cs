using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftCodeHealthAdapter(
    GitWorkingCopyProvider workingCopyProvider,
    IGitRepositoryReader gitRepositoryReader) : ISourceCraftCodeHealthSource
{
    public async Task<SourceCraftResult<CodeHealthReport>> GetCodeHealthAsync(string repositoryId, CancellationToken cancellationToken)
    {
        var clone = await workingCopyProvider.AcquireAsync(repositoryId, cancellationToken);
        var workingCopy = clone.Data;
        if (clone.Status != DataStatus.Available || workingCopy is null)
            return new SourceCraftResult<CodeHealthReport>(clone.Status, null, clone.Reason);

        using (workingCopy)
        {
            var report = await gitRepositoryReader.GetCodeHealthAsync(workingCopy.Path, cancellationToken);
            return new SourceCraftResult<CodeHealthReport>(DataStatus.Available, report, null);
        }
    }
}
