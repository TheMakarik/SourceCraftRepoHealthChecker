using FakeItEasy;
using FluentAssertions;
using SourceCraftRepoHealthChecker.Application.Rating.UseCases;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.UnitTests.Rating.UseCases;

public sealed class GetRepositoryReviewInsightsUseCaseTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_WhenMergeRequestsReviewed_ReturnsAverageAndMedianTimeToFirstReview()
    {
        // Arrange
        var source = CreateSource(
            CreateMergeRequest("alice", firstResponseAfterDays: 2, reviewComments: 3),
            CreateMergeRequest("bob", firstResponseAfterDays: 4, reviewComments: 1));
        var systemUnderTests = new GetRepositoryReviewInsightsUseCase(source);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Status.Should().Be(DataStatus.Available);
        actual.Data!.AverageTimeToFirstReviewDays.Should().Be(3);
        actual.Data.MedianTimeToFirstReviewDays.Should().Be(3);
        actual.Data.ReviewedMergeRequests.Should().Be(2);
        actual.Data.MergeRequestsWithoutReview.Should().Be(0);
        actual.Data.ShareOfCommentedMergeRequests.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_WhenSomeMergeRequestsHaveNoReview_ReturnsUnreviewedCountAndShare()
    {
        // Arrange
        var source = CreateSource(
            CreateMergeRequest("alice", firstResponseAfterDays: 1, reviewComments: 2),
            CreateMergeRequest("bob", firstResponseAfterDays: null, reviewComments: 0),
            CreateMergeRequest("carol", firstResponseAfterDays: null, reviewComments: 0, firstResponseEvaluated: false));
        var systemUnderTests = new GetRepositoryReviewInsightsUseCase(source);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Data!.TotalMergeRequests.Should().Be(3);
        actual.Data.EvaluatedMergeRequests.Should().Be(2);
        actual.Data.ReviewedMergeRequests.Should().Be(1);
        actual.Data.MergeRequestsWithoutReview.Should().Be(1);
        actual.Data.ShareOfCommentedMergeRequests.Should().Be(0.5);
    }

    [Fact]
    public async Task GetAsync_WhenUnevaluatedMergeRequestHasComments_ExcludesItFromReviewStatistics()
    {
        // Arrange
        var source = CreateSource(
            CreateMergeRequest("alice", firstResponseAfterDays: 1, reviewComments: 2, firstResponseEvaluated: false));
        var systemUnderTests = new GetRepositoryReviewInsightsUseCase(source);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Data!.EvaluatedMergeRequests.Should().Be(0);
        actual.Data.ReviewedMergeRequests.Should().Be(0);
        actual.Data.AverageTimeToFirstReviewDays.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenMultipleAuthors_ReturnsReviewerLoadOrderedByCount()
    {
        // Arrange
        var source = CreateSource(
            CreateMergeRequest("alice", firstResponseAfterDays: 1, reviewComments: 2),
            CreateMergeRequest("alice", firstResponseAfterDays: null, reviewComments: 0),
            CreateMergeRequest("bob", firstResponseAfterDays: 3, reviewComments: 1));
        var systemUnderTests = new GetRepositoryReviewInsightsUseCase(source);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        var alice = actual.Data!.ReviewerLoad.Single(item => item.AuthorLogin == "alice");
        alice.MergeRequestCount.Should().Be(2);
        alice.UnreviewedMergeRequestCount.Should().Be(1);
        alice.AverageTimeToFirstReviewDays.Should().Be(1);
        actual.Data.ReviewerLoad.Select(item => item.AuthorLogin).Should().ContainInOrder("alice", "bob");
    }

    [Fact]
    public async Task GetAsync_WhenSourceUnavailable_ReturnsSameStatusWithoutData()
    {
        // Arrange
        var source = A.Fake<ISourceCraftCollaborationSource>();
        A.CallTo(() => source.GetMergeRequestsAsync("sc-1", A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<MergeRequestInfo>>(DataStatus.Unavailable, null, "api failed"));
        var systemUnderTests = new GetRepositoryReviewInsightsUseCase(source);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.Status.Should().Be(DataStatus.Unavailable);
        actual.Data.Should().BeNull();
        actual.Reason.Should().Be("api failed");
    }

    [Fact]
    public async Task GetAsync_WhenSourceIsPartial_PropagatesPartialFlag()
    {
        // Arrange
        var source = A.Fake<ISourceCraftCollaborationSource>();
        A.CallTo(() => source.GetMergeRequestsAsync("sc-1", A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<MergeRequestInfo>>(
                DataStatus.Available,
                [CreateMergeRequest("alice", firstResponseAfterDays: 1, reviewComments: 1)],
                null,
                IsPartial: true));
        var systemUnderTests = new GetRepositoryReviewInsightsUseCase(source);

        // Act
        var actual = await systemUnderTests.GetAsync("sc-1", CancellationToken.None);

        // Assert
        actual.IsPartial.Should().BeTrue();
    }

    private static ISourceCraftCollaborationSource CreateSource(params MergeRequestInfo[] mergeRequests)
    {
        var source = A.Fake<ISourceCraftCollaborationSource>();
        A.CallTo(() => source.GetMergeRequestsAsync("sc-1", A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<MergeRequestInfo>>(DataStatus.Available, mergeRequests, null));
        return source;
    }

    private static MergeRequestInfo CreateMergeRequest(
        string authorLogin,
        int? firstResponseAfterDays,
        int reviewComments,
        bool firstResponseEvaluated = true) =>
        new(
            Guid.NewGuid().ToString(),
            "title",
            MergeRequestState.Open,
            authorLogin,
            BaseTime,
            null,
            null,
            firstResponseAfterDays is null ? null : BaseTime.AddDays(firstResponseAfterDays.Value),
            reviewComments,
            firstResponseEvaluated);
}
