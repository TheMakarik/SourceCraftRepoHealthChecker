using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.infrastructure.Authentication;
using SourceCraftRepoHealthChecker.infrastructure.Options;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;
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
        });

        services.AddSingleton<IAiTokenProtector, AiTokenProtector>();
        services.AddSingleton<ISourceCraftAccessTokenAccessor, SourceCraftAccessTokenAccessor>();
        services.AddHostedService<ScheduledAnalysisBackgroundService>();

        services.Scan(scan => scan
            .FromAssemblyOf<SourceCraftHttpClient>()
            .AddClasses(classes => classes.Where(type => type.Name.EndsWith("Adapter", StringComparison.Ordinal)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }
}
