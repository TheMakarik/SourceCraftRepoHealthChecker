namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public interface IGetCurrentUserUseCase
{
    public Task<AuthenticatedUser?> GetAsync(Guid userId, CancellationToken cancellationToken);
}
