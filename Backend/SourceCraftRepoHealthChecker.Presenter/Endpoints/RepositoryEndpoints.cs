using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Rating.Models;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.UseCases;
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

        endpoints.MapGet("/api/repositories/languages", async (IGetRepositoryLanguagesUseCase useCase, CancellationToken cancellationToken) =>
            Results.Ok(await useCase.GetAsync(cancellationToken)));

        endpoints.MapGet("/api/repositories/{id}/analysis", async (string id, HttpContext context, IGetRepositoryAnalysisUseCase useCase, CancellationToken cancellationToken) =>
        {
            var analysis = await useCase.GetAsync(id, cancellationToken);
            if (analysis is null)
                return Results.NotFound();
            if (analysis.IsPrivate && analysis.OwnerUserId != context.GetCurrentUserId())
                return Results.NotFound();

            return Results.Ok(analysis);
        });

        endpoints.MapGet("/api/repositories/{id}/history", async (string id, IGetRepositoryHistoryUseCase useCase, CancellationToken cancellationToken) =>
            Results.Ok(await useCase.GetAsync(id, cancellationToken)));

        endpoints.MapGet("/api/repositories/{id}/structure", async (string id, ISourceCraftStructureSource source, CancellationToken cancellationToken) =>
            Results.Ok(await source.GetStructureAsync(id, cancellationToken)));

        endpoints.MapGet("/api/repositories/{id}/tree", async (string id, string? path, bool? recursive, IGetRepositoryTreeUseCase useCase, CancellationToken cancellationToken) =>
        {
            var result = await useCase.GetAsync(id, path ?? string.Empty, recursive ?? false, cancellationToken);
            return Results.Ok(result.Data ?? new RepositoryTree([], false));
        });

        endpoints.MapGet("/api/repositories/{id}/file", async (string id, string path, IGetRepositoryFileUseCase useCase, CancellationToken cancellationToken) =>
        {
            var result = await useCase.GetAsync(id, path, cancellationToken);
            return result.Data is null ? Results.NotFound() : Results.Ok(result.Data);
        });

        endpoints.MapGet("/api/repositories/{id}/folders", async (string id, IGetRepositoryFoldersUseCase useCase, CancellationToken cancellationToken) =>
        {
            var result = await useCase.GetAsync(id, cancellationToken);
            return Results.Ok(result.Data ?? []);
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
