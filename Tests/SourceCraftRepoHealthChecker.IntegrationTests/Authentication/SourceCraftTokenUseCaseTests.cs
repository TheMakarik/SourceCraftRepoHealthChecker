using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Authentication.UseCases;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;
using SourceCraftRepoHealthChecker.infrastructure.Options;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;
using SourceCraftRepoHealthChecker.infrastructure.Security;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Authentication;

public sealed class SourceCraftTokenUseCaseTests : IDisposable
{
    private static readonly string TestKey = Convert.ToBase64String(new byte[32]);

    private readonly SqliteConnection _connection;
    private readonly RepoHealthCheckerDbContext _context;
    private readonly AiTokenProtector _protector;
    private readonly StoreSourceCraftTokenUseCase _storeUseCase;
    private readonly ResolveSourceCraftTokenUseCase _resolveUseCase;
    private readonly GetCurrentUserUseCase _getCurrentUserUseCase;

    public SourceCraftTokenUseCaseTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = TestDbContextFactory.Create(_connection);
        _context.Database.EnsureCreated();
        _protector = new AiTokenProtector(Options.Create(new AiTokenEncryptionOptions { Key = TestKey }));
        _storeUseCase = new StoreSourceCraftTokenUseCase(_context, _protector, CreateUserOptions());
        _resolveUseCase = new ResolveSourceCraftTokenUseCase(_context, _protector, NullLogger<ResolveSourceCraftTokenUseCase>.Instance);
        _getCurrentUserUseCase = new GetCurrentUserUseCase(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task StoreAsync_ThenResolveAsync_ReturnsOriginalTokenAndPersistsOnlyCiphertext()
    {
        // Arrange
        var user = AddUser("ya-1", "alice");

        // Act
        var stored = await _storeUseCase.StoreAsync(user.Id, "sc-pat-secret", CancellationToken.None);
        var actual = await _resolveUseCase.ResolveAsync(user.Id, CancellationToken.None);
        var persisted = await _context.Users.SingleAsync(item => item.Id == user.Id);

        // Assert
        stored.Should().BeTrue();
        actual.Should().Be("sc-pat-secret");
        persisted.SourceCraftToken.Should().NotBeNull().And.NotBe("sc-pat-secret");
    }

    [Fact]
    public async Task StoreAsync_WhenUserUnknown_ReturnsFalse()
    {
        // Act
        var actual = await _storeUseCase.StoreAsync(Guid.NewGuid(), "sc-pat-secret", CancellationToken.None);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_WhenNoTokenStored_ReturnsNull()
    {
        // Arrange
        var user = AddUser("ya-2", "bob");

        // Act
        var actual = await _resolveUseCase.ResolveAsync(user.Id, CancellationToken.None);

        // Assert
        actual.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenUserExists_ReturnsAuthenticatedUser()
    {
        // Arrange
        var user = AddUser("ya-3", "carol");

        // Act
        var actual = await _getCurrentUserUseCase.GetAsync(user.Id, CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual!.UserId.Should().Be(user.Id);
        actual.Login.Should().Be("carol");
    }

    private User AddUser(string yaId, string login)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            YaId = yaId,
            Login = login,
            DisplayName = login,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private static IOptions<UserOptions> CreateUserOptions() =>
        Options.Create(new UserOptions
        {
            MaxYaIdLength = 64,
            MaxLoginLength = 64,
            MaxDisplayNameLength = 128,
            MaxEmailLength = 256,
            MaxSourceCraftTokenLength = 4096
        });
}
