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
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SRHC_TEST_POSTGRES")
        ?? "Host=127.0.0.1;Port=55432;Database=sourcecraft_repo_health;Username=postgres;Password=postgres";

    public const string InternalToken = "test-internal-token";

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
        builder.UseSetting("Authentication:FrontendRedirectUrl", "");
        builder.UseSetting("SourceCraftServiceOptions:InternalToken", InternalToken);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IXmlRepository>();
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(_keyDirectory));

            RegisterSourceCraftStubs(services);
        });
    }

    public void ResetDatabase()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RepoHealthCheckerDbContext>();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();
    }

    public void SeedRepository(string sourceCraftId, bool isPrivate, Guid? ownerId, string name = "demo")
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RepoHealthCheckerDbContext>();
        if (ownerId is not null)
        {
            dbContext.Users.Add(new User
            {
                Id = ownerId.Value,
                YaId = $"ya-{ownerId.Value:N}",
                Login = $"owner-{ownerId.Value:N}",
                DisplayName = "Owner",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
            });
        }

        dbContext.Repositories.Add(new Repository
        {
            Id = Guid.NewGuid(),
            SourceCraftId = sourceCraftId,
            Name = name,
            FullName = $"owner/{name}",
            Url = $"https://sourcecraft.dev/owner/{name}",
            Language = "C#",
            IsPrivate = isPrivate,
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastActivityAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(_keyDirectory))
            Directory.Delete(_keyDirectory, recursive: true);
    }

    private static void RegisterSourceCraftStubs(IServiceCollection services)
    {
        var repository = new SourceCraftRepository("r1", "demo", "owner/demo", "https://sourcecraft.dev/owner/demo", "C#", 5, DateTimeOffset.Parse("2026-01-10T00:00:00Z"), false, "main");
        var user = new SourceCraftUser("u1", "alice", "Alice", null);

        var catalog = A.Fake<ISourceCraftRepositoryCatalog>();
        A.CallTo(() => catalog.GetRepositoryAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<SourceCraftRepository>(DataStatus.Available, repository, null)));
        A.CallTo(() => catalog.GetOpenRepositoriesAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<IReadOnlyCollection<SourceCraftRepository>>(DataStatus.Available, [], null)));

        var authentication = A.Fake<ISourceCraftAuthentication>();
        A.CallTo(() => authentication.GetAuthorizationUrlAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new Uri("https://oauth.example/authorize")));
        A.CallTo(() => authentication.CompleteAuthorizationAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(user));
        A.CallTo(() => authentication.GetCurrentUserAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(user));
        A.CallTo(() => authentication.GetAvailableRepositoriesAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<SourceCraftRepository>>([]));

        var activity = A.Fake<ISourceCraftActivitySource>();
        A.CallTo(() => activity.GetCommitActivityAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<CommitActivity>(DataStatus.Available, new CommitActivity(3, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-10T00:00:00Z"), new Dictionary<DateOnly, int> { [new DateOnly(2026, 1, 10)] = 3 }), null)));
        A.CallTo(() => activity.GetContributorsAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<IReadOnlyCollection<Contributor>>(DataStatus.Available, [], null)));
        A.CallTo(() => activity.GetReleasesAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<IReadOnlyCollection<ReleaseInfo>>(DataStatus.Available, [], null)));

        var collaboration = A.Fake<ISourceCraftCollaborationSource>();
        A.CallTo(() => collaboration.GetIssuesAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<IReadOnlyCollection<IssueInfo>>(DataStatus.Available, [], null)));
        A.CallTo(() => collaboration.GetMergeRequestsAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<IReadOnlyCollection<MergeRequestInfo>>(DataStatus.Available, [], null)));

        var security = A.Fake<ISourceCraftSecuritySource>();
        A.CallTo(() => security.GetFindingsAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<IReadOnlyCollection<SecurityFinding>>(DataStatus.Available, [], null)));

        var pipeline = A.Fake<ISourceCraftPipelineSource>();
        A.CallTo(() => pipeline.GetPipelineRunsAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<IReadOnlyCollection<PipelineRun>>(DataStatus.Available, [], null)));

        var codeHealth = A.Fake<ISourceCraftCodeHealthSource>();
        A.CallTo(() => codeHealth.GetCodeHealthAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<CodeHealthReport>(DataStatus.Available, new CodeHealthReport(1, 0, 1, null), null)));

        var documentation = A.Fake<ISourceCraftDocumentationSource>();
        A.CallTo(() => documentation.GetDocumentationAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<DocumentationReport>(DataStatus.Available, new DocumentationReport(true, true, false, false, true, true), null)));

        var structure = A.Fake<ISourceCraftStructureSource>();
        A.CallTo(() => structure.GetStructureAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new SourceCraftResult<RepositoryStructureReport>(DataStatus.Available, new RepositoryStructureReport(10, 3, 2, 2, "src", 5), null)));

        services.RemoveAll<ISourceCraftRepositoryCatalog>();
        services.RemoveAll<ISourceCraftAuthentication>();
        services.RemoveAll<ISourceCraftActivitySource>();
        services.RemoveAll<ISourceCraftCollaborationSource>();
        services.RemoveAll<ISourceCraftSecuritySource>();
        services.RemoveAll<ISourceCraftPipelineSource>();
        services.RemoveAll<ISourceCraftCodeHealthSource>();
        services.RemoveAll<ISourceCraftDocumentationSource>();
        services.RemoveAll<ISourceCraftStructureSource>();

        services.AddSingleton(catalog);
        services.AddSingleton(authentication);
        services.AddSingleton(activity);
        services.AddSingleton(collaboration);
        services.AddSingleton(security);
        services.AddSingleton(pipeline);
        services.AddSingleton(codeHealth);
        services.AddSingleton(documentation);
        services.AddSingleton(structure);
    }
}
