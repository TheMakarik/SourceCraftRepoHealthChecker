using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

        endpoints.MapGet("/readyz", async (RepoHealthCheckerDbContext dbContext, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("Readiness");
            try
            {
                if (await dbContext.Database.CanConnectAsync(cancellationToken))
                    return Results.Ok(new { status = "ready" });

                logger.LogWarning("Readiness probe failed: database is not reachable");
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Readiness probe failed: database connectivity check threw");
            }

            return Results.Json(
                new { status = "degraded", reason = "database_unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        return endpoints;
    }
}
