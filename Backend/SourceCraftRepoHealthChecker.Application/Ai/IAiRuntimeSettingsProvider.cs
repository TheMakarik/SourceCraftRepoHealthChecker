namespace SourceCraftRepoHealthChecker.Application.Ai;

public interface IAiRuntimeSettingsProvider
{
    public Task<AiRuntimeSettings?> GetAsync(Guid userId, CancellationToken cancellationToken);
}
