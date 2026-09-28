using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public sealed class GetAiSettingsUseCase(IRepoHealthCheckerDbContext dbContext) : IGetAiSettingsUseCase
{
    public async Task<AiSettingsResult?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userAi = (await dbContext.UserAis.Where(item => item.UserId == userId).ToListAsync(cancellationToken)).FirstOrDefault();
        if (userAi is null)
            return null;

        return new AiSettingsResult(userAi.AiProvider, userAi.AiBaseUrl, userAi.AiModel);
    }
}
