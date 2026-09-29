using SourceCraftRepoHealthChecker.Application.Ai.Models;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public interface IGetAiTokenOverviewUseCase
{
    public Task<IReadOnlyCollection<AiProviderToken>> GetAsync(Guid userId, CancellationToken cancellationToken);
}
