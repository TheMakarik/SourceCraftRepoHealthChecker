using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;

public interface ISourceCraftAuthentication
{
    public Task<Uri> GetAuthorizationUrlAsync(string state, CancellationToken cancellationToken);

    public Task<SourceCraftUser> CompleteAuthorizationAsync(string authorizationCode, string state, CancellationToken cancellationToken);

    public Task<SourceCraftUser> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken);

    public Task<IReadOnlyCollection<SourceCraftRepository>> GetAvailableRepositoriesAsync(string accessToken, CancellationToken cancellationToken);
}
