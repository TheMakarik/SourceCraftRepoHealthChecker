using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Rating.Models;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Application.SourceCraft.UseCases;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.Presenter.Authentication;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class RepositoryEndpoints
{
    public static IEndpointRouteBuilder MapRepositoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/repositories/refresh", async (HttpContext context, IOptions<SourceCraftServiceOptions> options, IRefreshRepositoriesUseCase useCase, CancellationToken cancellationToken) =>
        {
            var authenticated = context.GetCurrentUserId() is not null;
            if (!authenticated && !IsAuthorizedInternalRequest(context, options.Value))
                return Results.Unauthorized();

            return Results.Ok(new { refreshed = await useCase.RefreshAsync(cancellationToken) });
        });

        endpoints.MapGet("/api/repositories", async (string[]? language, string? sort, int? page, int? pageSize, bool? hasCi, int? minScore, int? maxScore, IGetRepositoryLeaderboardUseCase useCase, CancellationToken cancellationToken) =>
        {
            var query = new RepositoryLeaderboardQuery(language ?? [], ParseSort(sort), page ?? 1, pageSize ?? 20, hasCi, minScore, maxScore);
            return Results.Ok(await useCase.GetAsync(query, cancellationToken));
        });

        endpoints.MapReviewInsightsEndpoints();

        endpoints.MapGet("/api/repositories/languages", async (IGetRepositoryLanguagesUseCase useCase, CancellationToken cancellationToken) =>
            Results.Ok(await useCase.GetAsync(cancellationToken)));

        endpoints.MapGet("/api/repositories/{id}/analysis", async (string id, HttpContext context, RepositoryAccessGuard guard, IGetRepositoryAnalysisUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (await guard.EvaluateAsync(id, context, cancellationToken) == RepositoryAccessDecision.Forbidden)
                return Results.NotFound();

            var analysis = await useCase.GetAsync(id, cancellationToken);
            return analysis is null ? Results.NotFound() : Results.Ok(analysis);
        });

        endpoints.MapGet("/api/repositories/{id}/history", async (string id, HttpContext context, RepositoryAccessGuard guard, IGetRepositoryHistoryUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (await guard.EvaluateAsync(id, context, cancellationToken) == RepositoryAccessDecision.Forbidden)
                return Results.NotFound();

            return Results.Ok(await useCase.GetAsync(id, cancellationToken));
        });

        endpoints.MapGet("/api/repositories/{id}/structure", async (string id, HttpContext context, RepositoryAccessGuard guard, ISourceCraftStructureSource source, CancellationToken cancellationToken) =>
        {
            if (await guard.EvaluateAsync(id, context, cancellationToken) == RepositoryAccessDecision.Forbidden)
                return Results.NotFound();

            return Results.Ok(await source.GetStructureAsync(id, cancellationToken));
        });

        endpoints.MapGet("/api/repositories/{id}/tree", async (string id, string? path, bool? recursive, HttpContext context, RepositoryAccessGuard guard, IGetRepositoryTreeUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (await guard.EvaluateAsync(id, context, cancellationToken) == RepositoryAccessDecision.Forbidden)
                return Results.NotFound();

            var result = await useCase.GetAsync(id, path ?? string.Empty, recursive ?? false, cancellationToken);
            if (result.Data is not null)
                return Results.Ok(result.Data);

            return result.Status == DataStatus.Unavailable
                ? SourceUnavailable(result.Reason)
                : Results.Ok(new RepositoryTree([], false));
        });

        endpoints.MapGet("/api/repositories/{id}/file", async (string id, string path, HttpContext context, RepositoryAccessGuard guard, IGetRepositoryFileUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (await guard.EvaluateAsync(id, context, cancellationToken) == RepositoryAccessDecision.Forbidden)
                return Results.NotFound();

            var result = await useCase.GetAsync(id, path, cancellationToken);
            return result.Data is null ? Results.NotFound() : Results.Ok(result.Data);
        });

        endpoints.MapGet("/api/repositories/{id}/folders", async (string id, HttpContext context, RepositoryAccessGuard guard, IGetRepositoryFoldersUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (await guard.EvaluateAsync(id, context, cancellationToken) == RepositoryAccessDecision.Forbidden)
                return Results.NotFound();

            var result = await useCase.GetAsync(id, cancellationToken);
            if (result.Data is not null)
                return Results.Ok(result.Data);

            return result.Status == DataStatus.Unavailable
                ? SourceUnavailable(result.Reason)
                : Results.Ok((IReadOnlyList<RepositoryFolderAnalysis>)[]);
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

    private static IResult SourceUnavailable(string? reason) =>
        Results.Json(
            new { error = "source_unavailable", status = nameof(DataStatus.Unavailable), reason },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static bool IsAuthorizedInternalRequest(HttpContext context, SourceCraftServiceOptions options)
    {
        if (string.IsNullOrEmpty(options.InternalToken))
            return false;

        var providedToken = context.Request.Headers["X-Internal-Token"].ToString();
        if (string.IsNullOrEmpty(providedToken))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(options.InternalToken),
            Encoding.UTF8.GetBytes(providedToken));
    }
}
