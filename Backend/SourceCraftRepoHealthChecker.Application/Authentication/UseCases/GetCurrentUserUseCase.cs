using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;

namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public sealed class GetCurrentUserUseCase(IRepoHealthCheckerDbContext dbContext) : IGetCurrentUserUseCase
{
    public async Task<AuthenticatedUser?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user is null
            ? null
            : new AuthenticatedUser(user.Id, user.YaId, user.Login, user.DisplayName);
    }
}
