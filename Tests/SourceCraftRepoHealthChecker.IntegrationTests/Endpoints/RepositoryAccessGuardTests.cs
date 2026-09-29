using FluentAssertions;
using SourceCraftRepoHealthChecker.Presenter.Endpoints;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Endpoints;

public sealed class RepositoryAccessGuardTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    public void IsAccessible_WhenCombination_ReturnsExpected(bool isPrivate, bool hasOwner, bool hasUser, bool expected)
    {
        // Arrange
        var ownerId = hasOwner ? OwnerId : (Guid?)null;
        Guid? currentUserId = hasUser ? (hasOwner ? OwnerId : OtherUserId) : null;

        // Act
        var actual = RepositoryAccessGuard.IsAccessible(isPrivate, ownerId, currentUserId);

        // Assert
        actual.Should().Be(expected);
    }
}
