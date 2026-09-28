using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class CompareEndpoints
{
    private const int MinimumRepositories = 2;
    private const int MaximumRepositories = 4;

    public static IEndpointRouteBuilder MapCompareEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/repositories/compare", async (string? ids, ICompareRepositoriesUseCase useCase, CancellationToken cancellationToken) =>
        {
            var sourceCraftIds = ParseIds(ids);
            if (sourceCraftIds.Count < MinimumRepositories || sourceCraftIds.Count > MaximumRepositories)
                return Results.BadRequest(new { error = "invalid_ids_count", minimum = MinimumRepositories, maximum = MaximumRepositories });

            return Results.Ok(await useCase.GetAsync(sourceCraftIds, cancellationToken));
        });

        return endpoints;
    }

    private static IReadOnlyCollection<string> ParseIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return [];

        return ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToArray();
    }
}
