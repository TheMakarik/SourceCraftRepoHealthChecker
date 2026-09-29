using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Application.Ai.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;

namespace SourceCraftRepoHealthChecker.Application.Ai.UseCases;

public sealed class TestAiConnectionUseCase(
    IAiRuntimeSettingsProvider settingsProvider,
    IChatClientFactory chatClientFactory,
    IOptions<AiOptions> options) : ITestAiConnectionUseCase
{
    public async Task<AiTestResult> TestAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await settingsProvider.GetAsync(userId, cancellationToken);
        if (settings is null)
            return new AiTestResult(false, "AI-провайдер не настроен: выберите провайдера и модель, затем сохраните токен.");

        try
        {
            using var chatClient = chatClientFactory.Create(settings.Provider, settings.BaseUrl, settings.Model, settings.Token);
            var completion = await chatClient.GetResponseAsync(options.Value.TestPrompt, chatClientFactory.CreateOptions(settings.Provider), cancellationToken);
            var answer = AiCompletionText.Best(completion);
            return new AiTestResult(true, string.IsNullOrWhiteSpace(answer) ? "Модель вернула пустой ответ." : answer.Trim());
        }
        catch (Exception exception)
        {
            return new AiTestResult(false, $"Не удалось получить ответ: {exception.Message}");
        }
    }
}
