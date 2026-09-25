using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public static class TestDbContextFactory
{
    public static RepoHealthCheckerDbContext Create(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<RepoHealthCheckerDbContext>()
            .UseSqlite(connection)
            .Options;

        return new RepoHealthCheckerDbContext(
            options,
            Options.Create(new UserOptions { MaxYaIdLength = 64, MaxLoginLength = 64, MaxDisplayNameLength = 128, MaxEmailLength = 256 }),
            Options.Create(new UserAiOptions { MaxBaseUrlLength = 512, MaxModelLength = 128, MaxTokenLength = 4096 }),
            Options.Create(new RepositoryOptions { MaxSourceCraftIdLength = 128, MaxNameLength = 256, MaxFullNameLength = 512, MaxUrlLength = 1024, MaxLanguageLength = 64 }),
            Options.Create(new RecommendationOptions { MaxTitleLength = 256, MaxProblemLength = 2048, MaxActionLength = 2048, MaxExpectedImpactLength = 1024, MaxSourceReferenceLength = 512 }));
    }
}
