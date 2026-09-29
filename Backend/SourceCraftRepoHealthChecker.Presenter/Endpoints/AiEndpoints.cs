using System.Text.Json;
using SourceCraftRepoHealthChecker.Application.Ai.Models;
using SourceCraftRepoHealthChecker.Application.Ai.UseCases;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.Presenter.Authentication;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/me/ai", async (HttpContext context, IGetAiSettingsUseCase useCase, CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();
            if (userId is null)
                return Results.Unauthorized();

            var settings = await useCase.GetAsync(userId.Value, cancellationToken);
            return settings is null ? Results.NoContent() : Results.Ok(settings);
        });

        endpoints.MapPut("/api/me/ai", async (StoreAiSettingsRequest request, HttpContext context, IStoreAiSettingsUseCase useCase, CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();
            if (userId is null)
                return Results.Unauthorized();

            await useCase.StoreAsync(userId.Value, request.Provider, request.BaseUrl, request.Model, request.Token, cancellationToken);
            return Results.NoContent();
        });

        endpoints.MapGet("/api/me/ai/models", (IGetAiModelsUseCase useCase) => Results.Ok(useCase.Get()));

        endpoints.MapPost("/api/me/ai/test", async (HttpContext context, ITestAiConnectionUseCase useCase, CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();
            if (userId is null)
                return Results.Unauthorized();

            return Results.Ok(await useCase.TestAsync(userId.Value, cancellationToken));
        });

        endpoints.MapPost("/api/repositories/{id}/ai-summary", async (string id, HttpContext context, IAiSummaryUseCase useCase, CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();
            if (userId is null)
                return Results.Unauthorized();

            return Results.Ok(await useCase.SummarizeAsync(id, userId.Value, cancellationToken));
        });

        endpoints.MapPost("/api/repositories/{id}/ai-insights/{kind}", async (string id, string kind, HttpContext context, IAiInsightUseCase useCase, CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();
            if (userId is null)
                return Results.Unauthorized();

            if (!Enum.TryParse<AiInsightKind>(kind.Replace("-", string.Empty), ignoreCase: true, out var insightKind))
                return Results.BadRequest(new { error = "unknown_insight_kind", available = Enum.GetNames<AiInsightKind>() });

            return Results.Ok(await useCase.GenerateAsync(id, userId.Value, insightKind, cancellationToken));
        });

        endpoints.MapPost("/api/repositories/{id}/ai-summary/stream", (string id, HttpContext context, IAiStreamUseCase useCase, CancellationToken cancellationToken) =>
            StreamAiAsync(context, useCase, id, null, cancellationToken));

        endpoints.MapPost("/api/repositories/{id}/ai-insights/{kind}/stream", (string id, string kind, HttpContext context, IAiStreamUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<AiInsightKind>(kind.Replace("-", string.Empty), ignoreCase: true, out var insightKind))
                return Task.FromResult(Results.BadRequest(new { error = "unknown_insight_kind", available = Enum.GetNames<AiInsightKind>() }));

            return StreamAiAsync(context, useCase, id, insightKind, cancellationToken);
        });

        return endpoints;
    }

    private static Task<IResult> StreamAiAsync(HttpContext context, IAiStreamUseCase useCase, string id, AiInsightKind? kind, CancellationToken cancellationToken)
    {
        var userId = context.GetCurrentUserId();
        if (userId is null)
            return Task.FromResult(Results.Unauthorized());

        IResult result = Results.Stream(async stream =>
        {
            var writer = new StreamWriter(stream) { AutoFlush = true };
            try
            {
                await foreach (var streamEvent in useCase.StreamAsync(id, userId.Value, kind, cancellationToken))
                    await WriteEventAsync(writer, streamEvent, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                await WriteEventAsync(writer, new AiStreamEvent("error", exception.Message), CancellationToken.None);
            }
        }, "text/event-stream");

        return Task.FromResult(result);
    }

    private static async Task WriteEventAsync(StreamWriter writer, AiStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        await writer.WriteAsync($"data: {JsonSerializer.Serialize(streamEvent)}\n\n");
        await writer.FlushAsync(cancellationToken);
    }
}
