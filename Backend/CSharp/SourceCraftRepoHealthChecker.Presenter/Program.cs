using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using SourceCraftRepoHealthChecker.Application;
using SourceCraftRepoHealthChecker.Application.Authentication.UseCases;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Rating.Models;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.infrastructure;
using SourceCraftRepoHealthChecker.Presenter;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Async(sink => sink.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Code)));

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.UseSerilogRequestLogging();

app.Use(async (context, next) =>
{
    var accessor = context.RequestServices.GetRequiredService<ISourceCraftAccessTokenAccessor>();
    accessor.Token = ExtractBearer(context.Request);
    await next();
});

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/repositories/refresh", async (IRefreshRepositoriesUseCase useCase, CancellationToken cancellationToken) =>
    Results.Ok(new { refreshed = await useCase.RefreshAsync(cancellationToken) }));

app.MapGet("/api/repositories", async (string? language, string? sort, int? page, int? pageSize, IGetRepositoryLeaderboardUseCase useCase, CancellationToken cancellationToken) =>
{
    var query = new RepositoryLeaderboardQuery(language, ParseSort(sort), page ?? 1, pageSize ?? 20);
    return Results.Ok(await useCase.GetAsync(query, cancellationToken));
});

app.MapGet("/api/repositories/{id}/analysis", async (string id, IGetRepositoryAnalysisUseCase useCase, CancellationToken cancellationToken) =>
{
    var analysis = await useCase.GetAsync(id, cancellationToken);
    return analysis is null ? Results.NotFound() : Results.Ok(analysis);
});

app.MapGet("/api/repositories/{id}/report.md", async (string id, IExportRepositoryReportUseCase useCase, CancellationToken cancellationToken) =>
{
    var markdown = await useCase.GetMarkdownAsync(id, cancellationToken);
    return markdown is null ? Results.NotFound() : Results.Text(markdown, "text/markdown; charset=utf-8");
});

app.MapGet("/auth/login", async (HttpContext context, IAuthenticateUserUseCase useCase, CancellationToken cancellationToken) =>
{
    var state = Guid.NewGuid().ToString("N");
    context.Response.Cookies.Append("oauth_state", state, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = context.Request.IsHttps });
    var url = await useCase.StartAsync(state, cancellationToken);
    return Results.Redirect(url.ToString());
});

app.MapGet("/auth/callback", async (string code, string state, HttpContext context, IAuthenticateUserUseCase useCase, CancellationToken cancellationToken) =>
{
    var expectedState = context.Request.Cookies["oauth_state"];
    if (string.IsNullOrEmpty(expectedState) || expectedState != state)
        return Results.BadRequest(new { error = "invalid_state" });

    var user = await useCase.CompleteAsync(code, state, cancellationToken);
    context.Response.Cookies.Append("user_id", user.UserId.ToString(), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = context.Request.IsHttps });
    context.Response.Cookies.Delete("oauth_state");
    return Results.Ok(user);
});

app.MapGet("/api/me", async (HttpContext context, IRepoHealthCheckerDbContext dbContext, CancellationToken cancellationToken) =>
{
    var cookie = context.Request.Cookies["user_id"];
    if (!Guid.TryParse(cookie, out var userId))
        return Results.Unauthorized();

    var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
    return user is null
        ? Results.NotFound()
        : Results.Ok(new { user.Id, user.YaId, user.Login, user.DisplayName });
});

app.MapGet("/api/me/repositories", async (HttpContext context, IGetUserRepositoriesUseCase useCase, CancellationToken cancellationToken) =>
{
    var token = ExtractBearer(context.Request);
    if (string.IsNullOrEmpty(token))
        return Results.Unauthorized();

    return Results.Ok(await useCase.GetAsync(token, cancellationToken));
});

app.MapPost("/api/me/repositories/{id}/analyze", async (string id, HttpContext context, IAnalyzeRepositoryUseCase useCase, CancellationToken cancellationToken) =>
{
    Guid? userId = Guid.TryParse(context.Request.Cookies["user_id"], out var parsed) ? parsed : null;
    var result = await useCase.AnalyzeAsync(new AnalyzeRepositoryRequest(id, userId), cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/rating", async (string? language, string? sort, int? page, IGetRepositoryLeaderboardUseCase useCase, CancellationToken cancellationToken) =>
{
    var query = new RepositoryLeaderboardQuery(language, ParseSort(sort), page ?? 1, 20);
    var result = await useCase.GetAsync(query, cancellationToken);
    return Results.Content(RatingPageRenderer.Render(result, language, SortKey(query.Sort)), "text/html; charset=utf-8");
});

app.MapGet("/repositories/{id}", async (string id, IGetRepositoryAnalysisUseCase useCase, CancellationToken cancellationToken) =>
{
    var analysis = await useCase.GetAsync(id, cancellationToken);
    return analysis is null ? Results.NotFound() : Results.Content(AnalysisPageRenderer.Render(analysis), "text/html; charset=utf-8");
});

app.Run();

static RepositoryLeaderboardSort ParseSort(string? sort) => sort?.ToLowerInvariant() switch
{
    "likes" => RepositoryLeaderboardSort.Likes,
    "activity" => RepositoryLeaderboardSort.Activity,
    _ => RepositoryLeaderboardSort.Score
};

static string SortKey(RepositoryLeaderboardSort sort) => sort switch
{
    RepositoryLeaderboardSort.Likes => "likes",
    RepositoryLeaderboardSort.Activity => "activity",
    _ => "score"
};

static string? ExtractBearer(HttpRequest request)
{
    var header = request.Headers.Authorization.ToString();
    return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
}
