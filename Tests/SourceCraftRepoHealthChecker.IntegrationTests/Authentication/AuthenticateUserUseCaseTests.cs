using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Authentication.UseCases;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Authentication;

public sealed class AuthenticateUserUseCaseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly RepoHealthCheckerDbContext _context;

    public AuthenticateUserUseCaseTests()
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
    public async Task CompleteAsync_WhenSameUserLogsInTwice_UpsertsWithoutDuplicatesAndKeepsUserId()
    {
        // Arrange
        var authentication = new StubSourceCraftAuthentication(
            new SourceCraftUser("ya-42", "alice", "Alice", null),
            new Uri("https://oauth.yandex.ru/authorize"));
        var systemUnderTests = CreateUseCase(authentication);

        // Act
        var first = await systemUnderTests.CompleteAsync("code-1", "state-1", CancellationToken.None);
        var second = await systemUnderTests.CompleteAsync("code-2", "state-2", CancellationToken.None);
        var actual = await _context.Users.ToListAsync();

        // Assert
        first.UserId.Should().Be(second.UserId);
        actual.Should().ContainSingle();
        actual[0].YaId.Should().Be("ya-42");
        actual[0].Login.Should().Be("alice");
        actual[0].DisplayName.Should().Be("Alice");
        actual[0].LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_WhenCalled_ReturnsAuthorizationUrlFromAdapter()
    {
        // Arrange
        var expectedUrl = new Uri("https://oauth.yandex.ru/authorize?state=state-1");
        var authentication = new StubSourceCraftAuthentication(
            new SourceCraftUser("ya-42", "alice", "Alice", null),
            expectedUrl);
        var systemUnderTests = CreateUseCase(authentication);

        // Act
        var actual = await systemUnderTests.StartAsync("state-1", CancellationToken.None);

        // Assert
        actual.Should().Be(expectedUrl);
    }

    private AuthenticateUserUseCase CreateUseCase(StubSourceCraftAuthentication authentication) => new(
        authentication,
        _context,
        Options.Create(new UserOptions
        {
            MaxYaIdLength = 64,
            MaxLoginLength = 64,
            MaxDisplayNameLength = 128,
            MaxEmailLength = 256
        }),
        new FixedTimeProvider(DateTimeOffset.UtcNow));
}
