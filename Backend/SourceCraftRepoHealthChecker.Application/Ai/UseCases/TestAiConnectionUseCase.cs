using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Application.Ai.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public sealed class TestAiConnectionUseCase(
    IRepoHealthCheckerDbContext dbContext,
    IChatClientFactory chatClientFactory,
    ISecretProtector secretProtector,
    IOptions<AiOptions> options) : ITestAiConnectionUseCase
{
    public async Task<AiTestResult> TestAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userAi = (await dbContext.UserAis.Where(item => item.UserId == userId).ToListAsync(cancellationToken)).FirstOrDefault();
        if (userAi is null || string.IsNullOrWhiteSpace(userAi.AiToken))
            return new AiTestResult(false, "AI-провайдер не настроен: выберите провайдера и модель, затем сохраните токен.");

        try
        {
            using var chatClient = chatClientFactory.Create(userAi.AiProvider, userAi.AiBaseUrl, userAi.AiModel, secretProtector.Unprotect(userAi.AiToken));
            var completion = await chatClient.GetResponseAsync(options.Value.TestPrompt, chatClientFactory.CreateOptions(userAi.AiProvider), cancellationToken);
            return new AiTestResult(true, string.IsNullOrWhiteSpace(completion.Text) ? "Модель вернула пустой ответ." : completion.Text.Trim());
        }
        catch (Exception exception)
        {
            return new AiTestResult(false, $"Не удалось получить ответ: {exception.Message}");
        }
    }
}
