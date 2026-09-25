using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Authentication;

public sealed class StubSourceCraftAuthentication(SourceCraftUser user, Uri authorizationUrl) : ISourceCraftAuthentication
{
    public Task<Uri> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken) =>
        Task.FromResult(authorizationUrl);

    public Task<SourceCraftUser> CompleteAuthorizationAsync(string authorizationCode, string state, CancellationToken cancellationToken) =>
        Task.FromResult(user);

    public Task<SourceCraftUser> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken) =>
        Task.FromResult(user);

    public Task<IReadOnlyCollection<SourceCraftRepository>> GetAvailableRepositoriesAsync(string accessToken, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<SourceCraftRepository>>([]);
}
