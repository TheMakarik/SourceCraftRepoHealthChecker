using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

namespace SourceCraftRepoHealthChecker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HealthCheckOptions>().Bind(configuration.GetSection(nameof(HealthCheckOptions)));

        services.AddSingleton<IMetricNormalizer, MetricNormalizer>();
        services.AddSingleton<ICategoryScoreCalculator, CategoryScoreCalculator>();
        services.AddSingleton<IHealthScoreCalculator, HealthScoreCalculator>();
        services.AddSingleton<IRecommendationGenerator, RecommendationGenerator>();
        services.AddSingleton<IHealthCheckEngine, HealthCheckEngine>();
        services.AddScoped<IAnalyzeRepositoryUseCase, AnalyzeRepositoryUseCase>();

        return services;
    }
}
