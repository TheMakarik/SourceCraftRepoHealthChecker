using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using SourceCraftRepoHealthChecker.Application;
using SourceCraftRepoHealthChecker.Application.Analysis;
using SourceCraftRepoHealthChecker.Application.Configuration;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.infrastructure;
using SourceCraftRepoHealthChecker.infrastructure.Analysis;
using SourceCraftRepoHealthChecker.infrastructure.Configuration;
using SourceCraftRepoHealthChecker.infrastructure.DataProtection;
using SourceCraftRepoHealthChecker.infrastructure.Options;
using SourceCraftRepoHealthChecker.Presenter.Authentication;
using SourceCraftRepoHealthChecker.Presenter.Endpoints;

IEnvironmentFileLoader environmentFileLoader = new DotEnvEnvironmentFileLoader();
environmentFileLoader.Load(Path.Join(Directory.GetCurrentDirectory(), ".env"));
environmentFileLoader.Load(Path.Join(AppContext.BaseDirectory, ".env"));

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Async(sink => sink.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Code)));

builder.Services.AddRepoHealthDataProtection(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAnalysisStatusHub, AnalysisStatusHub>();
builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<UserTicketProtector>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddScoped<RepositoryAccessGuard>();

var corsAllowedOrigins = builder.Configuration.GetSection("Cors").Get<CorsOptions>()?.AllowedOrigins ?? [];
const string corsPolicyName = "FrontendCors";
if (corsAllowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy(corsPolicyName, policy => policy
        .WithOrigins(corsAllowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
}

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseWebSockets();

if (corsAllowedOrigins.Length > 0)
    app.UseCors(corsPolicyName);

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
app.MapCompareEndpoints();
app.MapIntegrityEndpoints();
app.MapOwnershipEndpoints();
app.MapPublicEndpoints();

app.MapGet("/ws/analysis", async (HttpContext context, IAnalysisStatusHub statusHub) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
        return Results.BadRequest(new { error = "websocket_required" });

    var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var cancellationToken = context.RequestAborted;

    var requestedProtocols = context.WebSockets.WebSocketRequestedProtocols;
    using var socket = requestedProtocols.Count > 0
        ? await context.WebSockets.AcceptWebSocketAsync(requestedProtocols[0])
        : await context.WebSockets.AcceptWebSocketAsync();

    var guard = context.RequestServices.GetRequiredService<RepositoryAccessGuard>();
    var accessibleRepositoryIds = await guard.GetAccessibleRepositoryIdsAsync(context, cancellationToken);

    var snapshot = statusHub.GetSnapshot().Where(statusEvent => accessibleRepositoryIds.Contains(statusEvent.RepositoryId));
    await SendMessageAsync(socket, new { type = "snapshot", items = snapshot }, serializerOptions, cancellationToken);

    var channel = Channel.CreateUnbounded<AnalysisStatusEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    using var subscription = statusHub.Subscribe(statusEvent =>
    {
        if (!accessibleRepositoryIds.Contains(statusEvent.RepositoryId))
            return true;

        return channel.Writer.TryWrite(statusEvent);
    });

    var sendTask = SendStatusEventsAsync(socket, channel.Reader, serializerOptions, cancellationToken);

    var buffer = new byte[1024];
    while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
    {
        var receiveResult = await socket.ReceiveAsync(buffer, cancellationToken);
        if (receiveResult.MessageType == WebSocketMessageType.Close)
            break;
    }

    channel.Writer.TryComplete();
    try
    {
        await sendTask;
    }
    catch (OperationCanceledException)
    {
        // The client disconnected; the socket is already being closed.
    }

    if (socket.State == WebSocketState.CloseReceived)
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None);

    return Results.Empty;
});

app.Run();

static async Task SendStatusEventsAsync(WebSocket socket, ChannelReader<AnalysisStatusEvent> reader, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
{
    await foreach (var statusEvent in reader.ReadAllAsync(cancellationToken))
    {
        var message = new
        {
            type = "status",
            repositoryId = statusEvent.RepositoryId,
            status = statusEvent.Status,
            score = statusEvent.Score,
            updatedAt = statusEvent.UpdatedAt
        };
        await SendMessageAsync(socket, message, serializerOptions, cancellationToken);
    }
}

static async Task SendMessageAsync<T>(WebSocket socket, T message, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
{
    var payload = JsonSerializer.SerializeToUtf8Bytes(message, serializerOptions);
    await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
}
