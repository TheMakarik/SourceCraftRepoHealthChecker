using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Rating.Models;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Presenter.Authentication;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class RepositoryEndpoints
{
    public static IEndpointRouteBuilder MapRepositoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/repositories/refresh", async (IRefreshRepositoriesUseCase useCase, CancellationToken cancellationToken) =>
            Results.Ok(new { refreshed = await useCase.RefreshAsync(cancellationToken) }));

        endpoints.MapGet("/api/repositories", async (string? language, string? sort, int? page, int? pageSize, IGetRepositoryLeaderboardUseCase useCase, CancellationToken cancellationToken) =>
        {
            var query = new RepositoryLeaderboardQuery(language, ParseSort(sort), page ?? 1, pageSize ?? 20);
            return Results.Ok(await useCase.GetAsync(query, cancellationToken));
        });

        endpoints.MapGet("/api/repositories/{id}/analysis", async (string id, HttpContext context, IGetRepositoryAnalysisUseCase useCase, CancellationToken cancellationToken) =>
        {
            var analysis = await useCase.GetAsync(id, cancellationToken);
            if (analysis is null)
                return Results.NotFound();
            if (analysis.IsPrivate && analysis.OwnerUserId != context.GetCurrentUserId())
                return Results.NotFound();

            return Results.Ok(analysis);
        });

        return endpoints;
    }

    public static RepositoryLeaderboardSort ParseSort(string? sort) => sort?.ToLowerInvariant() switch
    {
        "likes" => RepositoryLeaderboardSort.Likes,
        "activity" => RepositoryLeaderboardSort.Activity,
        _ => RepositoryLeaderboardSort.Score
    };

    public static string SortKey(RepositoryLeaderboardSort sort) => sort switch
    {
        RepositoryLeaderboardSort.Likes => "likes",
        RepositoryLeaderboardSort.Activity => "activity",
        _ => "score"
    };
}
