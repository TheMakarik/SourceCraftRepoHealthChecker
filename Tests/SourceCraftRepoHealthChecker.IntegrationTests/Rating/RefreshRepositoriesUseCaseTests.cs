using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;
using SourceCraftRepoHealthChecker.IntegrationTests.TestDoubles;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Rating;

public sealed class RefreshRepositoriesUseCaseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly RepoHealthCheckerDbContext _context;

    public RefreshRepositoriesUseCaseTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = TestDbContextFactory.Create(_connection);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RefreshAsync_WhenCalledTwice_UpsertsWithoutDuplicates()
    {
        // Arrange
        var catalog = new StubRepositoryCatalog(
        [
            new SourceCraftRepository("sc-1", "one", "owner/one", "https://sourcecraft.dev/owner/one", "C#", 11, DateTimeOffset.UtcNow, false, "main"),
            new SourceCraftRepository("sc-2", "two", "owner/two", "https://sourcecraft.dev/owner/two", "Go", 3, DateTimeOffset.UtcNow, false, "main")
        ]);
        var systemUnderTests = CreateUseCase(catalog);

        // Act
        var first = await systemUnderTests.RefreshAsync(CancellationToken.None);
        var second = await systemUnderTests.RefreshAsync(CancellationToken.None);
        var actual = await _context.Repositories.OrderBy(item => item.SourceCraftId).ToListAsync();

        // Assert
        first.Should().Be(2);
        second.Should().Be(2);
        actual.Should().HaveCount(2);
        actual.Select(item => item.SourceCraftId).Should().ContainInOrder("sc-1", "sc-2");
        actual[0].LikesCount.Should().Be(11);
    }

    private RefreshRepositoriesUseCase CreateUseCase(StubRepositoryCatalog catalog) => new(
        _context,
        catalog,
        Options.Create(new RepositoryOptions
        {
            MaxSourceCraftIdLength = 128,
            MaxNameLength = 256,
            MaxFullNameLength = 512,
            MaxUrlLength = 1024,
            MaxLanguageLength = 64
        }),
        TimeProvider.System,
        NullLogger<RefreshRepositoriesUseCase>.Instance);
}
