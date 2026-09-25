namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public interface IAuthenticateUserUseCase
{
    public Task<Uri> StartAsync(string state, CancellationToken cancellationToken);

    public Task<AuthenticatedUser> CompleteAsync(string code, string state, CancellationToken cancellationToken);
}
