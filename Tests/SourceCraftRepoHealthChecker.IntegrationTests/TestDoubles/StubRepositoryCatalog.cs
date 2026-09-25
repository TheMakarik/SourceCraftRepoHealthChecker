using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.IntegrationTests.TestDoubles;

public sealed class StubRepositoryCatalog(IReadOnlyCollection<SourceCraftRepository> repositories) : ISourceCraftRepositoryCatalog
{
    public Task<SourceCraftResult<IReadOnlyCollection<SourceCraftRepository>>> GetOpenRepositoriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SourceCraftResult<IReadOnlyCollection<SourceCraftRepository>>(DataStatus.Available, repositories, null));

    public Task<SourceCraftResult<SourceCraftRepository>> GetRepositoryAsync(string repositoryId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
