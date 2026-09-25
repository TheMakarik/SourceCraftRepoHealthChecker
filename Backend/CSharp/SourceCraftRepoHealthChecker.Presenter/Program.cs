using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using SourceCraftRepoHealthChecker.Application;
using SourceCraftRepoHealthChecker.infrastructure;

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

app.Run();
