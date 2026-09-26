using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Scheduling;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.infrastructure.Authentication;
using SourceCraftRepoHealthChecker.infrastructure.DataProtection;
using SourceCraftRepoHealthChecker.infrastructure.Options;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;
using SourceCraftRepoHealthChecker.infrastructure.Reports;
using SourceCraftRepoHealthChecker.infrastructure.Scheduling;
using SourceCraftRepoHealthChecker.infrastructure.Security;
using SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

namespace SourceCraftRepoHealthChecker.infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SourceCraftServiceOptions>().Bind(configuration.GetSection(nameof(SourceCraftServiceOptions)));
        services.AddOptions<UserOptions>().Bind(configuration.GetSection(nameof(UserOptions)));
        services.AddOptions<UserAiOptions>().Bind(configuration.GetSection(nameof(UserAiOptions)));
        services.AddOptions<RepositoryOptions>().Bind(configuration.GetSection(nameof(RepositoryOptions)));
        services.AddOptions<RecommendationOptions>().Bind(configuration.GetSection(nameof(RecommendationOptions)));
        services.AddOptions<AiTokenEncryptionOptions>().Bind(configuration.GetSection(nameof(AiTokenEncryptionOptions)));
        services.AddOptions<DataProtectionStorageOptions>().Bind(configuration.GetSection(nameof(DataProtectionStorageOptions)));
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(nameof(DatabaseOptions)));

        services.AddDbContext<RepoHealthCheckerDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));
        services.AddScoped<IRepoHealthCheckerDbContext>(provider => provider.GetRequiredService<RepoHealthCheckerDbContext>());

        services.AddHttpClient<SourceCraftHttpClient>((provider, client) =>
        {
            var sourceCraftOptions = provider.GetRequiredService<IOptions<SourceCraftServiceOptions>>().Value;
            client.BaseAddress = new Uri(sourceCraftOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(sourceCraftOptions.TimeoutSeconds);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.UseJitter = true;
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(150);
        });

        services.AddSingleton<AiTokenProtector>();
        services.AddSingleton<IAiTokenProtector>(provider => provider.GetRequiredService<AiTokenProtector>());
        services.AddSingleton<IMinioClient>(provider =>
        {
            var storage = provider.GetRequiredService<IOptions<DataProtectionStorageOptions>>().Value;
            return new MinioClient()
                .WithEndpoint(storage.Endpoint)
                .WithCredentials(storage.AccessKey, storage.SecretKey)
                .WithSSL(storage.UseSsl)
                .Build();
        });
        services.AddSingleton<IXmlRepository, S3XmlRepository>();
        services.AddHostedService<DatabaseMigrationHostedService>();
        services.AddSingleton<ISecretProtector>(provider => provider.GetRequiredService<AiTokenProtector>());
        services.AddSingleton<IReportPdfRenderer, QuestPdfReportRenderer>();
        services.AddSingleton<ISourceCraftAccessTokenAccessor, SourceCraftAccessTokenAccessor>();
        services.AddHostedService<ScheduledAnalysisBackgroundService>();
        services.AddSingleton<ISchedulerLease, PostgresSchedulerLease>();
        services.AddScoped<AnalysisWorker>();
        services.AddHostedService<AnalysisWorkerBackgroundService>();

        services.AddSingleton<IGitRepositoryReader, LocalGitRepositoryReader>();

        services.Scan(scan => scan
            .FromAssemblyOf<SourceCraftHttpClient>()
            .AddClasses(classes => classes.Where(type => type.Name.EndsWith("Adapter", StringComparison.Ordinal)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }
}
