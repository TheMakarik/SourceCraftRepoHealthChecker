using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class ReviewInsightsEndpoints
{
    public static IEndpointRouteBuilder MapReviewInsightsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/repositories/{id}/review-insights", async (string id, HttpContext context, RepositoryAccessGuard guard, IGetRepositoryReviewInsightsUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (await guard.EvaluateAsync(id, context, cancellationToken) == RepositoryAccessDecision.Forbidden)
                return Results.NotFound();

            return Results.Ok(await useCase.GetAsync(id, cancellationToken));
        });

        return endpoints;
    }
}
