namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public interface IStoreSourceCraftTokenUseCase
{
    public Task<bool> StoreAsync(Guid userId, string token, CancellationToken cancellationToken);
}
