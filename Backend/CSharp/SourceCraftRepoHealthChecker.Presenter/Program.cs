using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using SourceCraftRepoHealthChecker.Application;
using SourceCraftRepoHealthChecker.Application.Rating.Models;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
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

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/repositories/refresh", async (IRefreshRepositoriesUseCase useCase, CancellationToken cancellationToken) =>
    Results.Ok(new { refreshed = await useCase.RefreshAsync(cancellationToken) }));

app.MapGet("/api/repositories", async (string? language, string? sort, int? page, int? pageSize, IGetRepositoryLeaderboardUseCase useCase, CancellationToken cancellationToken) =>
{
    var query = new RepositoryLeaderboardQuery(language, ParseSort(sort), page ?? 1, pageSize ?? 20);
    return Results.Ok(await useCase.GetAsync(query, cancellationToken));
});

app.MapGet("/rating", async (string? language, string? sort, int? page, IGetRepositoryLeaderboardUseCase useCase, CancellationToken cancellationToken) =>
{
    var query = new RepositoryLeaderboardQuery(language, ParseSort(sort), page ?? 1, 20);
    var result = await useCase.GetAsync(query, cancellationToken);
    return Results.Content(RatingPageRenderer.Render(result, language, SortKey(query.Sort)), "text/html; charset=utf-8");
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
