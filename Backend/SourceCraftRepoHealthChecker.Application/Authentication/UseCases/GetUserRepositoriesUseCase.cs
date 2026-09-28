using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public sealed class GetUserRepositoriesUseCase(ISourceCraftAuthentication authentication) : IGetUserRepositoriesUseCase
{
    public Task<IReadOnlyCollection<SourceCraftRepository>> GetAsync(string accessToken, CancellationToken cancellationToken) =>
        authentication.GetAvailableRepositoriesAsync(accessToken, cancellationToken);
}
