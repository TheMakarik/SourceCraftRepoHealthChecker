using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public interface IStoreAiSettingsUseCase
{
    public Task StoreAsync(Guid userId, AiProviders provider, string? baseUrl, string model, string token, CancellationToken cancellationToken);
}
