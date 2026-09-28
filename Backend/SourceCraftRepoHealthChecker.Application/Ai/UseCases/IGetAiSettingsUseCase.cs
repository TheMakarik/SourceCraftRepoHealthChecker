using SourceCraftRepoHealthChecker.Application.Ai.Models;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public interface IGetAiSettingsUseCase
{
    public Task<AiSettingsResult?> GetAsync(Guid userId, CancellationToken cancellationToken);
}
