using SourceCraftRepoHealthChecker.Application.Ai.UseCases;
using SourceCraftRepoHealthChecker.Presenter.Authentication;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/me/ai", async (StoreAiSettingsRequest request, HttpContext context, IStoreAiSettingsUseCase useCase, CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();
            if (userId is null)
                return Results.Unauthorized();

            await useCase.StoreAsync(userId.Value, request.Provider, request.BaseUrl, request.Model, request.Token, cancellationToken);
            return Results.NoContent();
        });

        endpoints.MapPost("/api/repositories/{id}/ai-summary", async (string id, HttpContext context, IAiSummaryUseCase useCase, CancellationToken cancellationToken) =>
        {
            var userId = context.GetCurrentUserId();
            if (userId is null)
                return Results.Unauthorized();

            return Results.Ok(await useCase.SummarizeAsync(id, userId.Value, cancellationToken));
        });

        return endpoints;
    }
}
