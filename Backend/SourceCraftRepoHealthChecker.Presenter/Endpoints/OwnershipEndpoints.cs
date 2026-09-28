using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SourceCraftRepoHealthChecker.Application.Ownership.UseCases;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class OwnershipEndpoints
{
    public static IEndpointRouteBuilder MapOwnershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/repositories/{id}/ownership", async (string id, IGetRepositoryOwnershipUseCase useCase, CancellationToken cancellationToken) =>
            Results.Ok(await useCase.GetAsync(id, cancellationToken)));

        return endpoints;
    }
}
