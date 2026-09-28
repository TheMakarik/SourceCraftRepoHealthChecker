using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;

namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public sealed class ResolveSourceCraftTokenUseCase(
    IRepoHealthCheckerDbContext dbContext,
    ISecretProtector secretProtector,
    ILogger<ResolveSourceCraftTokenUseCase> logger) : IResolveSourceCraftTokenUseCase
{
    public async Task<string?> ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user?.SourceCraftToken is null)
            return null;

        try
        {
            return secretProtector.Unprotect(user.SourceCraftToken);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            logger.LogWarning(exception, "Stored SourceCraft token for user {UserId} could not be decrypted", userId);
            return null;
        }
    }
}
