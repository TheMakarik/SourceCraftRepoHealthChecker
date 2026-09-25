using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SourceCraftRepoHealthChecker.Application.Authentication.UseCases;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Services;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Application.Scheduling;
using SourceCraftRepoHealthChecker.Application.Scheduling.Options;

namespace SourceCraftRepoHealthChecker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HealthCheckOptions>().Bind(configuration.GetSection(nameof(HealthCheckOptions)));
        services.AddOptions<SchedulingOptions>().Bind(configuration.GetSection(nameof(SchedulingOptions)));

        services.AddSingleton<IMetricNormalizer, MetricNormalizer>();
        services.AddSingleton<ICategoryScoreCalculator, CategoryScoreCalculator>();
        services.AddSingleton<IHealthScoreCalculator, HealthScoreCalculator>();
        services.AddSingleton<IRecommendationGenerator, RecommendationGenerator>();
        services.AddSingleton<IHealthCheckEngine, HealthCheckEngine>();
        services.AddScoped<IAnalyzeRepositoryUseCase, AnalyzeRepositoryUseCase>();
        services.AddScoped<IGetRepositoryAnalysisUseCase, GetRepositoryAnalysisUseCase>();
        services.AddScoped<IExportRepositoryReportUseCase, ExportRepositoryReportUseCase>();
        services.AddScoped<IGetRepositoryLeaderboardUseCase, GetRepositoryLeaderboardUseCase>();
        services.AddScoped<IRefreshRepositoriesUseCase, RefreshRepositoriesUseCase>();
        services.AddScoped<IAuthenticateUserUseCase, AuthenticateUserUseCase>();
        services.AddScoped<IGetUserRepositoriesUseCase, GetUserRepositoriesUseCase>();
        services.AddScoped<IScheduledAnalysisRunner, ScheduledAnalysisRunner>();

        return services;
    }
}
