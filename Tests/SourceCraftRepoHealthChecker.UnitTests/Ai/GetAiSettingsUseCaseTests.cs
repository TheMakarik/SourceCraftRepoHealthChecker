using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Ai.UseCases;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.UnitTests.Scheduling.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.Ai;

public sealed class GetAiSettingsUseCaseTests
{
    [Fact]
    public async Task GetAsync_WhenSettingsExist_ReturnsSettingsWithoutToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var systemUnderTests = new GetAiSettingsUseCase(CreateDbContext(userId));

        // Act
        var actual = await systemUnderTests.GetAsync(userId, CancellationToken.None);

        // Assert
        actual.Should().NotBeNull();
        actual!.Provider.Should().Be(AiProviders.Yandex);
        actual.BaseUrl.Should().BeNull();
        actual.Model.Should().Be("yandexgpt");
    }

    [Fact]
    public async Task GetAsync_WhenSettingsAbsent_ReturnsNull()
    {
        // Arrange
        var systemUnderTests = new GetAiSettingsUseCase(CreateDbContext(userId: null));

        // Act
        var actual = await systemUnderTests.GetAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        actual.Should().BeNull();
    }

    private static IRepoHealthCheckerDbContext CreateDbContext(Guid? userId)
    {
        var userAis = userId is null
            ? new List<UserAi>()
            : [new UserAi { Id = Guid.NewGuid(), UserId = userId.Value, AiProvider = AiProviders.Yandex, AiModel = "yandexgpt", AiToken = "encrypted-token" }];

        var dbContext = A.Fake<IRepoHealthCheckerDbContext>();
        A.CallTo(() => dbContext.UserAis).Returns(CreateDbSet(userAis));
        return dbContext;
    }

    private static DbSet<UserAi> CreateDbSet(IReadOnlyList<UserAi> items)
    {
        var queryable = (IQueryable<UserAi>)new TestAsyncEnumerable<UserAi>(items);
        var dbSet = A.Fake<DbSet<UserAi>>(options => options.Implements(typeof(IQueryable<UserAi>)));
        A.CallTo(() => ((IQueryable<UserAi>)dbSet).Provider).Returns(queryable.Provider);
        A.CallTo(() => ((IQueryable<UserAi>)dbSet).Expression).Returns(queryable.Expression);
        A.CallTo(() => ((IQueryable<UserAi>)dbSet).ElementType).Returns(queryable.ElementType);
        A.CallTo(() => ((IQueryable<UserAi>)dbSet).GetEnumerator()).ReturnsLazily(() => queryable.GetEnumerator());
        return dbSet;
    }
}
