using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public sealed class GetAiTokenOverviewUseCase(
    IRepoHealthCheckerDbContext dbContext,
    ISecretProtector secretProtector) : IGetAiTokenOverviewUseCase
{
    private const int MaskedPrefixLength = 5;
    private const int MinimumTokenLengthForPrefix = 8;
    private const string HiddenToken = "****";

    public async Task<IReadOnlyCollection<AiProviderToken>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.UserAiTokens
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new AiProviderToken(row.Provider, Mask(Unprotect(row.Token))))
            .ToArray();
    }

    private string? Unprotect(string? protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
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
