using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Domain.Entities;

namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public sealed class AuthenticateUserUseCase(
    ISourceCraftAuthentication authentication,
    IRepoHealthCheckerDbContext dbContext,
    IOptions<UserOptions> userOptions,
    TimeProvider timeProvider) : IAuthenticateUserUseCase
{
    public Task<Uri> StartAsync(string state, CancellationToken cancellationToken) =>
        authentication.GetAuthorizationUrlAsync(state, cancellationToken);

    public async Task<AuthenticatedUser> CompleteAsync(string code, string state, CancellationToken cancellationToken)
    {
        var sourceCraftUser = await authentication.CompleteAuthorizationAsync(code, state, cancellationToken);
        var options = userOptions.Value;
        var yaId = Truncate(sourceCraftUser.Id, options.MaxYaIdLength);
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.YaId == yaId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                YaId = yaId,
                CreatedAt = now
            };
            dbContext.Users.Add(user);
        }

        user.Login = Truncate(sourceCraftUser.Login, options.MaxLoginLength);
        user.DisplayName = Truncate(sourceCraftUser.DisplayName, options.MaxDisplayNameLength);
        user.LastLoginAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthenticatedUser(user.Id, user.YaId, user.Login, user.DisplayName);
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
