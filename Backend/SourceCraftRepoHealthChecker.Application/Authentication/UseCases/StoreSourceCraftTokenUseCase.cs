using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;

namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public sealed class StoreSourceCraftTokenUseCase(
    IRepoHealthCheckerDbContext dbContext,
    ISecretProtector secretProtector,
    IOptions<UserOptions> userOptions) : IStoreSourceCraftTokenUseCase
{
    public async Task<bool> StoreAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
            return false;

        var protectedToken = secretProtector.Protect(token);
        user.SourceCraftToken = Truncate(protectedToken, userOptions.Value.MaxSourceCraftTokenLength);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
