using FakeItEasy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public sealed class LiveSourceCraftApiFactory : WebApplicationFactory<Program>
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SRHC_TEST_POSTGRES")
        ?? "Host=127.0.0.1;Port=55432;Database=sourcecraft_repo_health;Username=postgres;Password=postgres";

    private readonly string _keyDirectory = Path.Join(Path.GetTempPath(), "srhc-dp-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_keyDirectory);
        var token = Environment.GetEnvironmentVariable("SOURCECRAFT_PAT") ?? string.Empty;

        builder.UseSetting("AiTokenEncryptionOptions:Key", Convert.ToBase64String(new byte[32]));
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("SchedulingOptions:Enabled", "false");
        builder.UseSetting("ScalingOptions:WorkerEnabled", "false");
        builder.UseSetting("ScalingOptions:SchedulerEnabled", "false");
        builder.UseSetting("DatabaseOptions:AutoMigrate", "false");
        builder.UseSetting("Authentication:FrontendRedirectUrl", "");
        builder.UseSetting("SourceCraftServiceOptions:ServiceToken", token);
        builder.UseSetting("GitOptions:ServiceToken", token);
        builder.UseSetting("GitOptions:GitUsername", "git");
        builder.UseSetting("GitOptions:WorkDir", Path.GetTempPath());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IXmlRepository>();
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(_keyDirectory));

            var user = new SourceCraftUser("u1", "alice", "Alice", null);
            var authentication = A.Fake<ISourceCraftAuthentication>();
            A.CallTo(() => authentication.GetAuthorizationUrlAsync(A<string>._, A<CancellationToken>._))
                .Returns(Task.FromResult(new Uri("https://oauth.example/authorize")));
            A.CallTo(() => authentication.CompleteAuthorizationAsync(A<string>._, A<string>._, A<CancellationToken>._))
                .Returns(Task.FromResult(user));
            A.CallTo(() => authentication.GetCurrentUserAsync(A<string>._, A<CancellationToken>._))
                .Returns(Task.FromResult(user));
            A.CallTo(() => authentication.GetAvailableRepositoriesAsync(A<string>._, A<CancellationToken>._))
                .Returns(Task.FromResult<IReadOnlyCollection<SourceCraftRepository>>([]));
            services.RemoveAll<ISourceCraftAuthentication>();
            services.AddSingleton(authentication);
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
