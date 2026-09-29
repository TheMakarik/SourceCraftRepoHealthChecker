using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Options;
using SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

namespace SourceCraftRepoHealthChecker.UnitTests.SourceCraft;

public sealed class SourceCraftCollaborationAdapterTests
{
    private readonly ISourceCraftApi _sourceCraftApi = A.Fake<ISourceCraftApi>();

    [Fact]
    public async Task GetIssuesAsync_WhenResponseLookupsTruncated_ReturnsPartialAndMarksUnrequestedIssuesUnevaluated()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxItems: 1000, maxResponseLookups: 100, maxPages: 10);
        ArrangeIssuesPage(120, nextPageToken: null);
        ArrangeCommentsPage();

        // Act
        var actual = await systemUnderTests.GetIssuesAsync("repo-1", CancellationToken.None);

        // Assert
        actual.Status.Should().Be(DataStatus.Available);
        actual.IsPartial.Should().BeTrue();
        actual.Data.Should().HaveCount(120);
        actual.Data!.Take(100).Should().OnlyContain(issue => issue.FirstResponseEvaluated);
        actual.Data!.Skip(100).Should().OnlyContain(issue => !issue.FirstResponseEvaluated);
    }

    [Fact]
    public async Task GetIssuesAsync_WhenAllItemsAndResponsesRequested_ReturnsComplete()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxItems: 1000, maxResponseLookups: 100, maxPages: 10);
        ArrangeIssuesPage(10, nextPageToken: null);
        ArrangeCommentsPage();

        // Act
        var actual = await systemUnderTests.GetIssuesAsync("repo-1", CancellationToken.None);

        // Assert
        actual.IsPartial.Should().BeFalse();
        actual.Data.Should().HaveCount(10);
        actual.Data.Should().OnlyContain(issue => issue.FirstResponseEvaluated);
    }

    [Fact]
    public async Task GetIssuesAsync_WhenItemLimitTruncated_ReturnsPartial()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxItems: 3, maxResponseLookups: 100, maxPages: 10);
        ArrangeIssuesPage(5, nextPageToken: null);
        ArrangeCommentsPage();

        // Act
        var actual = await systemUnderTests.GetIssuesAsync("repo-1", CancellationToken.None);

        // Assert
        actual.IsPartial.Should().BeTrue();
        actual.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetIssuesAsync_WhenPageLimitTruncated_ReturnsPartial()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxItems: 1000, maxResponseLookups: 100, maxPages: 1);
        ArrangeIssuesPage(5, nextPageToken: "next-page");
        ArrangeCommentsPage();

        // Act
        var actual = await systemUnderTests.GetIssuesAsync("repo-1", CancellationToken.None);

        // Assert
        actual.IsPartial.Should().BeTrue();
    }

    [Fact]
    public async Task GetMergeRequestsAsync_WhenResponseLookupsTruncated_ReturnsPartialAndMarksUnrequestedRequestsUnevaluated()
    {
        // Arrange
        var systemUnderTests = CreateSystemUnderTests(maxItems: 1000, maxResponseLookups: 2, maxPages: 10);
        ArrangePullRequestsPage(4, nextPageToken: null);
        ArrangePullRequestCommentsPage();

        // Act
        var actual = await systemUnderTests.GetMergeRequestsAsync("repo-1", CancellationToken.None);

        // Assert
        actual.Status.Should().Be(DataStatus.Available);
        actual.IsPartial.Should().BeTrue();
        actual.Data.Should().HaveCount(4);
        actual.Data!.Take(2).Should().OnlyContain(request => request.FirstResponseEvaluated);
        actual.Data!.Skip(2).Should().OnlyContain(request => !request.FirstResponseEvaluated);
    }

    private SourceCraftCollaborationAdapter CreateSystemUnderTests(int maxItems, int maxResponseLookups, int maxPages) =>
        new(
            _sourceCraftApi,
            Options.Create(new SourceCraftServiceOptions
            {
                BaseUrl = "https://sourcecraft.test",
                ServiceToken = "token",
                TimeoutSeconds = 1,
                PageSize = 100,
                MaxItems = maxItems,
                MaxResponseLookups = maxResponseLookups,
                IssueCommentLimit = 20,
                Concurrency = 4,
                MaxPages = maxPages
            }));

    private void ArrangeIssuesPage(int count, string? nextPageToken)
    {
        var issues = Enumerable.Range(0, count).Select(CreateIssue).ToArray();
        A.CallTo(() => _sourceCraftApi.GetIssuesAsync("repo-1", "-created_at", 100, A<string?>._, A<CancellationToken>._))
            .Returns(new IssuePageDto { Issues = issues, NextPageToken = nextPageToken });
    }

    private void ArrangePullRequestsPage(int count, string? nextPageToken)
    {
        var pullRequests = Enumerable.Range(0, count).Select(CreatePullRequest).ToArray();
        A.CallTo(() => _sourceCraftApi.GetPullRequestsAsync("repo-1", "-created_at", 100, A<string?>._, A<CancellationToken>._))
            .Returns(new PullRequestPageDto { PullRequests = pullRequests, NextPageToken = nextPageToken });
    }

    private void ArrangeCommentsPage() =>
        A.CallTo(() => _sourceCraftApi.GetIssueCommentsAsync(A<string>._, "created_at", 100, A<string?>._, A<CancellationToken>._))
            .Returns(new IssueCommentPageDto { IssueComments = [], NextPageToken = null });

    private void ArrangePullRequestCommentsPage() =>
        A.CallTo(() => _sourceCraftApi.GetPullRequestCommentsAsync(A<string>._, "created_at", 100, A<string?>._, A<CancellationToken>._))
            .Returns(new PullRequestCommentPageDto { PullRequestComments = [], NextPageToken = null });

    private static IssueDto CreateIssue(int index) => new()
    {
        Id = $"issue-{index}",
        Title = $"Issue {index}",
        Author = new UserEmbeddedDto { Id = "author-1", Slug = "author" },
        CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        UpdatedAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z")
    };

    private static PullRequestDto CreatePullRequest(int index) => new()
    {
        Id = $"pr-{index}",
        Title = $"Pull request {index}",
        Author = new UserEmbeddedDto { Id = "author-1", Slug = "author" },
        Status = "open",
        CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        UpdatedAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z")
    };
}
