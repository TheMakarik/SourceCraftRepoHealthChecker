using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;

namespace SourceCraftRepoHealthChecker.IntegrationTests;

public sealed class RepoHealthCheckerDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly RepoHealthCheckerDbContext systemUnderTests;

    public RepoHealthCheckerDbContextTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        systemUnderTests = CreateContext(_connection);
        systemUnderTests.Database.EnsureCreated();
    }

    public void Dispose()
    {
        systemUnderTests.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserWithAiAdded_PersistsBothEntities()
    {
        // Arrange
        var user = new User
        {
            YaId = "ya-123",
            Login = "alice",
            DisplayName = "Alice Developer",
            Email = "alice@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.Ai = new UserAi
        {
            AiProvider = AiProviders.OpenAI,
            AiBaseUrl = "https://api.openai.com",
            AiModel = "gpt-4o",
            AiToken = "secret-token",
            CreatedAt = DateTimeOffset.UtcNow
        };
        systemUnderTests.Users.Add(user);

        // Act
        await systemUnderTests.SaveChangesAsync(CancellationToken.None);

        // Assert
        using var readContext = CreateContext(_connection);
        var actual = await readContext.Users
            .Include(item => item.Ai)
            .SingleAsync(item => item.YaId == "ya-123");

        actual.Login.Should().Be("alice");
        actual.Ai.Should().NotBeNull();
        actual.Ai!.AiModel.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenAnalysisHierarchyAdded_PersistsAllLevels()
    {
        // Arrange
        var repository = new Repository
        {
            SourceCraftId = "sc-42",
            Name = "sample",
            FullName = "alice/sample",
            Url = "https://sourcecraft.dev/alice/sample",
            Language = "C#",
            IsPrivate = false,
            LikesCount = 7,
            LastActivityAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var analysisRun = new AnalysisRun
        {
            Score = 82,
            Status = AnalysisStatus.Completed,
            DataStatus = DataStatus.Available,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        analysisRun.CategoryScores.Add(new CategoryScore
        {
            Category = ScoreCategory.CodeHealth,
            Score = 80,
            DataStatus = DataStatus.Available
        });
        analysisRun.CategoryScores.Add(new CategoryScore
        {
            Category = ScoreCategory.Documentation,
            Score = 70,
            DataStatus = DataStatus.NoData
        });
        analysisRun.Recommendations.Add(new Recommendation
        {
            Priority = RecommendationPriority.High,
            Title = "Add tests",
            Problem = "No automated tests found.",
            Action = "Add a test project and a CI step.",
            ExpectedImpact = "+10 code health",
            SourceReference = "tests/"
        });
        repository.AnalysisRuns.Add(analysisRun);
        systemUnderTests.Repositories.Add(repository);

        // Act
        await systemUnderTests.SaveChangesAsync(CancellationToken.None);

        // Assert
        using var readContext = CreateContext(_connection);
        var actual = await readContext.Repositories
            .Include(item => item.AnalysisRuns)
                .ThenInclude(run => run.CategoryScores)
            .Include(item => item.AnalysisRuns)
                .ThenInclude(run => run.Recommendations)
            .SingleAsync(item => item.SourceCraftId == "sc-42");

        actual.AnalysisRuns.Should().HaveCount(1);
        actual.AnalysisRuns.Single().CategoryScores.Should().HaveCount(2);
        actual.AnalysisRuns.Single().Recommendations.Should().HaveCount(1);
        actual.AnalysisRuns.Single().Recommendations.Single().Title.Should().Be("Add tests");
    }

    private static RepoHealthCheckerDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<RepoHealthCheckerDbContext>()
            .UseSqlite(connection)
            .Options;

        return new RepoHealthCheckerDbContext(
            options,
            Options.Create(new UserOptions
            {
                MaxYaIdLength = 64,
                MaxLoginLength = 64,
                MaxDisplayNameLength = 128,
                MaxEmailLength = 256
            }),
            Options.Create(new UserAiOptions
            {
                MaxBaseUrlLength = 512,
                MaxModelLength = 128,
                MaxTokenLength = 4096
            }),
            Options.Create(new RepositoryOptions
            {
                MaxSourceCraftIdLength = 128,
                MaxNameLength = 256,
                MaxFullNameLength = 512,
                MaxUrlLength = 1024,
                MaxLanguageLength = 64
            }),
            Options.Create(new RecommendationOptions
            {
                MaxTitleLength = 256,
                MaxProblemLength = 2048,
                MaxActionLength = 2048,
                MaxExpectedImpactLength = 1024,
                MaxSourceReferenceLength = 512
            }));
    }
}
