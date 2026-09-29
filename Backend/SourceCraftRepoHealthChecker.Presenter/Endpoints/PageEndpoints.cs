using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Rating.Models;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class PageEndpoints
{
    public static IEndpointRouteBuilder MapPageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/rating", async (string? language, string? sort, int? page, IGetRepositoryLeaderboardUseCase useCase, CancellationToken cancellationToken) =>
        {
            string[] languages = language is null ? [] : [language];
            var query = new RepositoryLeaderboardQuery(languages, RepositoryEndpoints.ParseSort(sort), page ?? 1, 20);
            var result = await useCase.GetAsync(query, cancellationToken);
            return Results.Content(RatingPageRenderer.Render(result, language, RepositoryEndpoints.SortKey(query.Sort)), "text/html; charset=utf-8");
        });

        endpoints.MapGet("/repositories/{id}", async (string id, HttpContext context, RepositoryAccessGuard guard, IGetRepositoryAnalysisUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (await guard.EvaluateAsync(id, context, cancellationToken) == RepositoryAccessDecision.Forbidden)
                return Results.NotFound();

            var analysis = await useCase.GetAsync(id, cancellationToken);
            return analysis is null ? Results.NotFound() : Results.Content(AnalysisPageRenderer.Render(analysis), "text/html; charset=utf-8");
        });

        return endpoints;
    }
}
