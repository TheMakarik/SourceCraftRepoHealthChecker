using System.Text.Json.Serialization;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using SourceCraftRepoHealthChecker.Application;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.infrastructure;
using SourceCraftRepoHealthChecker.Presenter.Authentication;
using SourceCraftRepoHealthChecker.Presenter.Endpoints;

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
builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDataProtection();
builder.Services.AddSingleton<UserTicketProtector>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    var accessor = context.RequestServices.GetRequiredService<ISourceCraftAccessTokenAccessor>();
    accessor.Token = context.Request.GetBearerToken();
    await next();
});

app.MapHealthEndpoints();
app.MapRepositoryEndpoints();
app.MapReportEndpoints();
app.MapAuthenticationEndpoints();
app.MapAiEndpoints();
app.MapPageEndpoints();

app.Run();
