using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class PublicEndpoints
{
    private const string BadgeCacheControl = "public, max-age=300, must-revalidate";
    private const string SvgContentType = "image/svg+xml; charset=utf-8";

    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/public/repositories/{id}/score", async (string id, IGetRepositoryAnalysisUseCase useCase, IOptions<HealthCheckOptions> healthCheckOptions, CancellationToken cancellationToken) =>
        {
            var analysis = await useCase.GetAsync(id, cancellationToken);
            if (analysis is null)
                return Results.NotFound();

            var publicScore = PublicRepositoryScoreFactory.Create(analysis, healthCheckOptions.Value.MethodologyVersion);
            return publicScore is null ? Results.NotFound() : Results.Ok(publicScore);
        });

        endpoints.MapGet("/api/public/repositories/{id}/badge.svg", async (string id, HttpContext context, IGetRepositoryAnalysisUseCase useCase, CancellationToken cancellationToken) =>
        {
            var analysis = await useCase.GetAsync(id, cancellationToken);
            if (analysis is null || analysis.IsPrivate)
                return Results.NotFound();

            context.Response.Headers[HeaderNames.CacheControl] = BadgeCacheControl;
            return Results.Text(RepositoryBadgeSvgRenderer.Render(analysis.Score), SvgContentType);
        });

        return endpoints;
    }
}
