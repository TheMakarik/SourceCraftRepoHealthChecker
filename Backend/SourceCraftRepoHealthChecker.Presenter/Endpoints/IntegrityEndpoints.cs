using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SourceCraftRepoHealthChecker.Application.Integrity.UseCases;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class IntegrityEndpoints
{
    public static IEndpointRouteBuilder MapIntegrityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/repositories/{id}/integrity", async (string id, HttpContext context, RepositoryAccessGuard guard, IComputeRepositoryIntegrityUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (await guard.EvaluateAsync(id, context, cancellationToken) == RepositoryAccessDecision.Forbidden)
                return Results.NotFound();

            var integrity = await useCase.GetAsync(id, cancellationToken);
            return integrity is null ? Results.NotFound() : Results.Ok(integrity);
        });

        return endpoints;
    }
}
