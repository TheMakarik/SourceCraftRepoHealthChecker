using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;

namespace SourceCraftRepoHealthChecker.Application.Ai;

public sealed class AiRuntimeSettingsProvider(
    IRepoHealthCheckerDbContext dbContext,
    ISecretProtector secretProtector) : IAiRuntimeSettingsProvider
{
    public async Task<AiRuntimeSettings?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userAi = (await dbContext.UserAis.Where(item => item.UserId == userId).ToListAsync(cancellationToken)).FirstOrDefault();
        if (userAi is null)
            return null;

        var tokenRow = (await dbContext.UserAiTokens
            .Where(item => item.UserId == userId && item.Provider == userAi.AiProvider)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        var protectedToken = tokenRow?.Token;
        if (string.IsNullOrWhiteSpace(protectedToken))
            protectedToken = userAi.AiToken;

        if (string.IsNullOrWhiteSpace(protectedToken))
            return null;

        return new AiRuntimeSettings(userAi.AiProvider, userAi.AiBaseUrl, userAi.AiModel, secretProtector.Unprotect(protectedToken));
    }
}
