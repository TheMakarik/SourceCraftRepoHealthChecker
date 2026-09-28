using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Integrity.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Presenter.Authentication;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class IntegrityEndpoints
{
    public static IEndpointRouteBuilder MapIntegrityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/repositories/{id}/integrity", async (string id, HttpContext context, IRepoHealthCheckerDbContext dbContext, IComputeRepositoryIntegrityUseCase useCase, CancellationToken cancellationToken) =>
        {
            var repository = await dbContext.Repositories.FirstOrDefaultAsync(item => item.SourceCraftId == id, cancellationToken);
            if (repository is null)
                return Results.NotFound();
            if (repository.IsPrivate && repository.OwnerId != context.GetCurrentUserId())
                return Results.NotFound();

            var integrity = await useCase.GetAsync(id, cancellationToken);
            return integrity is null ? Results.NotFound() : Results.Ok(integrity);
        });

        return endpoints;
    }
}
