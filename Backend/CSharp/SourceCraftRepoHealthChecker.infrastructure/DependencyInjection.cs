using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.infrastructure.Authentication;
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

        services.AddDbContext<RepoHealthCheckerDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
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
        services.AddSingleton<ISecretProtector>(provider => provider.GetRequiredService<AiTokenProtector>());
        services.AddSingleton<IReportPdfRenderer, QuestPdfReportRenderer>();
        services.AddSingleton<ISourceCraftAccessTokenAccessor, SourceCraftAccessTokenAccessor>();
        services.AddHostedService<ScheduledAnalysisBackgroundService>();

        services.AddSingleton<IGitRepositoryReader, LocalGitRepositoryReader>();

        services.Scan(scan => scan
            .FromAssemblyOf<SourceCraftHttpClient>()
            .AddClasses(classes => classes.Where(type => type.Name.EndsWith("Adapter", StringComparison.Ordinal)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }
}
