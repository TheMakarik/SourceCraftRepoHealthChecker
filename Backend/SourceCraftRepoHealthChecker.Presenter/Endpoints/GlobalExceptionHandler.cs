using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using SourceCraftRepoHealthChecker.Application.Ai;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.SourceCraft;
using SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

namespace SourceCraftRepoHealthChecker.Presenter.Endpoints;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = MapStatusCode(exception);
        var logLevel = statusCode >= StatusCodes.Status500InternalServerError ? LogLevel.Error : LogLevel.Warning;
        logger.Log(logLevel, exception, "Request {Path} failed with {StatusCode}", context.Request.Path, statusCode);

        if (context.Response.HasStarted)
            return true;

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = ReasonPhrase(statusCode) }, cancellationToken);
        return true;
    }

    private static int MapStatusCode(Exception exception) => exception switch
    {
        RepositoryAccessDeniedException => StatusCodes.Status403Forbidden,
        UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
        InvalidAuthorizationCodeException => StatusCodes.Status401Unauthorized,
        RepositoryNotFoundException => StatusCodes.Status404NotFound,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        ArgumentException => StatusCodes.Status400BadRequest,
        SourceCraftException => StatusCodes.Status503ServiceUnavailable,
        SourceCraftOperationException => StatusCodes.Status503ServiceUnavailable,
        AiProviderException => StatusCodes.Status503ServiceUnavailable,
        YandexIdUnavailableException => StatusCodes.Status503ServiceUnavailable,
        TimeoutException => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "bad_request",
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status503ServiceUnavailable => "source_unavailable",
        _ => "internal_error"
    };
}
