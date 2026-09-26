using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.SourceCraft;
using SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = MapStatusCode(exception);
        if (statusCode is null)
            return false;

        logger.LogWarning(exception, "Request {Path} failed with {StatusCode}", context.Request.Path, statusCode);
        context.Response.StatusCode = statusCode.Value;
        await context.Response.WriteAsJsonAsync(new { error = ReasonPhrase(statusCode.Value), message = exception.Message }, cancellationToken);
        return true;
    }

    private static int? MapStatusCode(Exception exception) => exception switch
    {
        RepositoryNotFoundException => StatusCodes.Status404NotFound,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        ArgumentException => StatusCodes.Status400BadRequest,
        SourceCraftException => StatusCodes.Status502BadGateway,
        SourceCraftOperationException => StatusCodes.Status502BadGateway,
        TimeoutException => StatusCodes.Status503ServiceUnavailable,
        _ => null
    };

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status400BadRequest => "bad_request",
        StatusCodes.Status502BadGateway => "source_unavailable",
        StatusCodes.Status503ServiceUnavailable => "service_unavailable",
        _ => "error"
    };
}
