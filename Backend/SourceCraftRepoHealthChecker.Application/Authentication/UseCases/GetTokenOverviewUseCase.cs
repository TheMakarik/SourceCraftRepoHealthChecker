using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;

namespace SourceCraftRepoHealthChecker.Application.Authentication.UseCases;

public sealed class GetTokenOverviewUseCase(
    IRepoHealthCheckerDbContext dbContext,
    ISecretProtector secretProtector) : IGetTokenOverviewUseCase
{
    private const int MaskedPrefixLength = 5;
    private const int MinimumTokenLengthForPrefix = 8;
    private const string HiddenToken = "****";

    public async Task<TokenOverview> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        var userAi = await dbContext.UserAis.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        string? aiProtected = null;
        if (userAi is not null)
        {
            var tokenRow = await dbContext.UserAiTokens.FirstOrDefaultAsync(
                item => item.UserId == userId && item.Provider == userAi.AiProvider,
                cancellationToken);
            aiProtected = tokenRow?.Token ?? userAi.AiToken;
        }

        return new TokenOverview(
            Mask(Unprotect(user?.SourceCraftToken)),
            Mask(Unprotect(aiProtected)));
    }

    private string? Unprotect(string? protectedSecret)
    {
        if (protectedSecret is null)
            return null;

        try
        {
            return secretProtector.Unprotect(protectedSecret);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return null;
        }
    }

    private static string? Mask(string? token)
    {
        if (token is null)
            return null;

        return token.Length < MinimumTokenLengthForPrefix ? HiddenToken : $"{token[..MaskedPrefixLength]}…";
    }
}
