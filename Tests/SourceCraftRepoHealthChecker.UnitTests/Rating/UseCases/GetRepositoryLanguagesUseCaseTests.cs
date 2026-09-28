using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SourceCraftRepoHealthChecker.Application.Persistence.Interfaces;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.UnitTests.Scheduling.TestDoubles;

namespace SourceCraftRepoHealthChecker.UnitTests.Rating.UseCases;

public sealed class GetRepositoryLanguagesUseCaseTests
{
    [Fact]
    public async Task GetAsync_WhenRepositoriesHaveLanguages_ReturnsDistinctSortedLanguages()
    {
        // Arrange
        var dbContext = CreateDbContext(
        [
            new Repository { Id = Guid.NewGuid(), SourceCraftId = "sc-1", Language = "csharp" },
            new Repository { Id = Guid.NewGuid(), SourceCraftId = "sc-2", Language = "TypeScript" },
            new Repository { Id = Guid.NewGuid(), SourceCraftId = "sc-3", Language = "csharp" },
            new Repository { Id = Guid.NewGuid(), SourceCraftId = "sc-4", Language = "PYTHON" },
            new Repository { Id = Guid.NewGuid(), SourceCraftId = "sc-5", Language = string.Empty },
            new Repository { Id = Guid.NewGuid(), SourceCraftId = "sc-6", Language = "   " }
        ]);
        var systemUnderTests = new GetRepositoryLanguagesUseCase(dbContext);

        // Act
        var actual = await systemUnderTests.GetAsync(CancellationToken.None);

        // Assert
        actual.Should().Equal("csharp", "PYTHON", "TypeScript");
    }

    [Fact]
    public async Task GetAsync_WhenNoLanguagesPresent_ReturnsEmpty()
    {
        // Arrange
        var dbContext = CreateDbContext(
        [
            new Repository { Id = Guid.NewGuid(), SourceCraftId = "sc-1", Language = string.Empty }
        ]);
        var systemUnderTests = new GetRepositoryLanguagesUseCase(dbContext);

        // Act
        var actual = await systemUnderTests.GetAsync(CancellationToken.None);

        // Assert
        actual.Should().BeEmpty();
    }

    private static IRepoHealthCheckerDbContext CreateDbContext(IReadOnlyList<Repository> repositories)
    {
        var dbContext = A.Fake<IRepoHealthCheckerDbContext>();
        A.CallTo(() => dbContext.Repositories).Returns(CreateDbSet(repositories));
        return dbContext;
    }

    private static DbSet<T> CreateDbSet<T>(IReadOnlyList<T> items) where T : class
    {
        var queryable = (IQueryable<T>)new TestAsyncEnumerable<T>(items);
        var dbSet = A.Fake<DbSet<T>>(options => options.Implements(typeof(IQueryable<T>)));
        A.CallTo(() => ((IQueryable<T>)dbSet).Provider).Returns(queryable.Provider);
        A.CallTo(() => ((IQueryable<T>)dbSet).Expression).Returns(queryable.Expression);
        A.CallTo(() => ((IQueryable<T>)dbSet).ElementType).Returns(queryable.ElementType);
        A.CallTo(() => ((IQueryable<T>)dbSet).GetEnumerator()).ReturnsLazily(() => queryable.GetEnumerator());
        return dbSet;
    }
}
