namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public interface IResolveSourceCraftTokenUseCase
{
    public Task<string?> ResolveAsync(Guid userId, CancellationToken cancellationToken);
}
