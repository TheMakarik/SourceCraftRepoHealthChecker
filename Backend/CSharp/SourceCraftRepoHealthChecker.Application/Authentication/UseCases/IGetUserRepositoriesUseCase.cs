using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public interface IGetUserRepositoriesUseCase
{
    public Task<IReadOnlyCollection<SourceCraftRepository>> GetAsync(string accessToken, CancellationToken cancellationToken);
}
