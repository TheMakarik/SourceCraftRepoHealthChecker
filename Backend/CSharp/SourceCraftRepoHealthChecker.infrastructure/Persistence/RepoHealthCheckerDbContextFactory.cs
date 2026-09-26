using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.Persistence;

public sealed class RepoHealthCheckerDbContextFactory : IDesignTimeDbContextFactory<RepoHealthCheckerDbContext>
{
    private const string ConnectionStringName = "DefaultConnection";
    private const string ConfigurationFileName = "appsettings.json";

    public RepoHealthCheckerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(ConfigurationFileName, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        var options = new DbContextOptionsBuilder<RepoHealthCheckerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RepoHealthCheckerDbContext(
            options,
            global::Microsoft.Extensions.Options.Options.Create(new UserOptions { MaxYaIdLength = 64, MaxLoginLength = 64, MaxDisplayNameLength = 128, MaxEmailLength = 256, MaxSourceCraftTokenLength = 4096 }),
            global::Microsoft.Extensions.Options.Options.Create(new UserAiOptions { MaxBaseUrlLength = 512, MaxModelLength = 128, MaxTokenLength = 4096 }),
            global::Microsoft.Extensions.Options.Options.Create(new RepositoryOptions { MaxSourceCraftIdLength = 128, MaxNameLength = 256, MaxFullNameLength = 512, MaxUrlLength = 512, MaxLanguageLength = 64 }),
            global::Microsoft.Extensions.Options.Options.Create(new RecommendationOptions { MaxTitleLength = 256, MaxProblemLength = 2048, MaxWhyImportantLength = 2048, MaxEvidenceLength = 2048, MaxActionLength = 2048, MaxExpectedImpactLength = 1024, MaxSourceReferenceLength = 512 }));
    }
}
