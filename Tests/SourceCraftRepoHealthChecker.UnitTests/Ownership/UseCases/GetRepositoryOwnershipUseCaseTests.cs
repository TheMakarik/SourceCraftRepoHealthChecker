using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Ownership.Interfaces;
using SourceCraftRepoHealthChecker.Application.Ownership.Models;
using SourceCraftRepoHealthChecker.Application.Ownership.Options;
using SourceCraftRepoHealthChecker.Application.Ownership.UseCases;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.UnitTests.Ownership.UseCases;

public sealed class GetRepositoryOwnershipUseCaseTests
{
    [Fact]
    public async Task GetAsync_WhenSingleOwnerCoversHalfOfCommits_ReturnsBusFactorOne()
    {
        // Arrange
        var source = A.Fake<IOwnershipSource>();
        A.CallTo(() => source.GetOwnersAsync("sc-1", A<CancellationToken>._))
            .Returns(Available(
                CreateOwner("alice", 8),
                CreateOwner("bob", 4),
                CreateOwner("carol", 3)));
        var systemUnderTests = CreateSystemUnderTests(source, coverageThresholdPercent: 50);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Data!.BusFactor.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_WhenCommitsSpreadEvenly_ReturnsBusFactorCoveringThreshold()
    {
        // Arrange
        var source = A.Fake<IOwnershipSource>();
        A.CallTo(() => source.GetOwnersAsync("sc-1", A<CancellationToken>._))
            .Returns(Available(
                CreateOwner("alice", 3),
                CreateOwner("bob", 3),
                CreateOwner("carol", 2),
                CreateOwner("dave", 2)));
        var systemUnderTests = CreateSystemUnderTests(source, coverageThresholdPercent: 50);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Data!.BusFactor.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_WhenOwnersProvided_ReturnsSortedOwnersAndTotalContributors()
    {
        // Arrange
        var source = A.Fake<IOwnershipSource>();
        A.CallTo(() => source.GetOwnersAsync("sc-1", A<CancellationToken>._))
            .Returns(Available(
                CreateOwner("carol", 1),
                CreateOwner("alice", 5),
                CreateOwner("bob", 3)));
        var systemUnderTests = CreateSystemUnderTests(source, coverageThresholdPercent: 50);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Data!.Owners.Select(owner => owner.Login).Should().ContainInOrder("alice", "bob", "carol");
        actual.Data.TotalContributors.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_WhenSourceUnavailable_ReturnsSameStatusWithoutData()
    {
        // Arrange
        var source = A.Fake<IOwnershipSource>();
        A.CallTo(() => source.GetOwnersAsync("sc-1", A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyList<OwnerStat>>(DataStatus.Unavailable, null, "clone failed"));
        var systemUnderTests = CreateSystemUnderTests(source, coverageThresholdPercent: 50);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Status.Should().Be(DataStatus.Unavailable);
        actual.Data.Should().BeNull();
        actual.Reason.Should().Be("clone failed");
    }

    [Fact]
    public async Task GetAsync_WhenNoOwners_ReturnsZeroBusFactor()
    {
        // Arrange
        var source = A.Fake<IOwnershipSource>();
        A.CallTo(() => source.GetOwnersAsync("sc-1", A<CancellationToken>._))
            .Returns(Available());
        var systemUnderTests = CreateSystemUnderTests(source, coverageThresholdPercent: 50);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Data!.BusFactor.Should().Be(0);
        actual.Data.TotalContributors.Should().Be(0);
    }

    private static GetRepositoryOwnershipUseCase CreateSystemUnderTests(IOwnershipSource source, int coverageThresholdPercent) =>
        new(source, Options.Create(new OwnershipOptions
        {
            CoverageThresholdPercent = coverageThresholdPercent,
            TopDirectoriesCount = 3
        }));

    private static SourceCraftResult<IReadOnlyList<OwnerStat>> Available(params OwnerStat[] owners) =>
        new(DataStatus.Available, owners, null);

    private static OwnerStat CreateOwner(string login, int commits) =>
        new(login, commits, commits, ["src"]);
}
