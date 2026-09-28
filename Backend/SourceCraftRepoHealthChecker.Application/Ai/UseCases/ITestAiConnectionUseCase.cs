using SourceCraftRepoHealthChecker.Application.Ai.Models;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public interface ITestAiConnectionUseCase
{
    public Task<AiTestResult> TestAsync(Guid userId, CancellationToken cancellationToken);
}
