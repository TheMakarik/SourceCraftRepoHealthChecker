using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public sealed class StoreAiSettingsUseCase(
    IRepoHealthCheckerDbContext dbContext,
    ISecretProtector secretProtector,
    TimeProvider timeProvider) : IStoreAiSettingsUseCase
{
    public async Task StoreAsync(Guid userId, AiProviders provider, string? baseUrl, string model, string token, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var userAi = (await dbContext.UserAis.Where(item => item.UserId == userId).ToListAsync(cancellationToken)).FirstOrDefault();

        if (userAi is null)
        {
            userAi = new UserAi { Id = Guid.NewGuid(), UserId = userId, CreatedAt = now };
            dbContext.UserAis.Add(userAi);
        }

        userAi.AiProvider = provider;
        userAi.AiBaseUrl = baseUrl;
        userAi.AiModel = model;
        userAi.UpdatedAt = now;

        if (!string.IsNullOrWhiteSpace(token))
        {
            var tokenRow = (await dbContext.UserAiTokens
                .Where(item => item.UserId == userId && item.Provider == provider)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();

            if (tokenRow is null)
            {
                tokenRow = new UserAiToken { Id = Guid.NewGuid(), UserId = userId, Provider = provider, CreatedAt = now };
                dbContext.UserAiTokens.Add(tokenRow);
            }

            tokenRow.Token = secretProtector.Protect(token);
            tokenRow.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
