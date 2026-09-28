using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;
using SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SRHC_TEST_POSTGRES")
        ?? "Host=127.0.0.1;Port=55432;Database=sourcecraft_repo_health;Username=postgres;Password=postgres";

    private readonly string _keyDirectory = Path.Join(Path.GetTempPath(), "srhc-dp-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_keyDirectory);

        builder.UseSetting("AiTokenEncryptionOptions:Key", Convert.ToBase64String(new byte[32]));
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("SchedulingOptions:Enabled", "false");
        builder.UseSetting("ScalingOptions:WorkerEnabled", "false");
        builder.UseSetting("ScalingOptions:SchedulerEnabled", "false");
        builder.UseSetting("DatabaseOptions:AutoMigrate", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IXmlRepository>();
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(_keyDirectory));

            services.AddHttpClient<SourceCraftHttpClient>()
                .ConfigureHttpClient(client => client.BaseAddress = new Uri("http://stub.local"))
                .ConfigurePrimaryHttpMessageHandler(() => new StubSourceCraftHandler());
        });
    }

    public void ResetDatabase()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RepoHealthCheckerDbContext>();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(_keyDirectory))
            Directory.Delete(_keyDirectory, recursive: true);
    }
}
