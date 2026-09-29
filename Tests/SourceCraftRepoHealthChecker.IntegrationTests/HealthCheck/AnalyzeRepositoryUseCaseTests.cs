using FakeItEasy;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;
using SourceCraftRepoHealthChecker.Application.Options;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Entities;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;
using SourceCraftRepoHealthChecker.infrastructure.Persistence;

namespace SourceCraftRepoHealthChecker.IntegrationTests.HealthCheck;

public sealed class AnalyzeRepositoryUseCaseTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly RepoHealthCheckerDbContext _context;
    private readonly ISourceCraftRepositoryCatalog _catalog = A.Fake<ISourceCraftRepositoryCatalog>();
    private readonly ISourceCraftActivitySource _activitySource = A.Fake<ISourceCraftActivitySource>();
    private readonly ISourceCraftCollaborationSource _collaborationSource = A.Fake<ISourceCraftCollaborationSource>();
    private readonly ISourceCraftSecuritySource _securitySource = A.Fake<ISourceCraftSecuritySource>();
    private readonly ISourceCraftPipelineSource _pipelineSource = A.Fake<ISourceCraftPipelineSource>();
    private readonly ISourceCraftCodeHealthSource _codeHealthSource = A.Fake<ISourceCraftCodeHealthSource>();
    private readonly ISourceCraftDocumentationSource _documentationSource = A.Fake<ISourceCraftDocumentationSource>();
    private readonly IHealthCheckEngine _healthCheckEngine = A.Fake<IHealthCheckEngine>();
    private readonly IAnomalyDetector _anomalyDetector = A.Fake<IAnomalyDetector>();
    private RepositoryFacts? _capturedFacts;

    public AnalyzeRepositoryUseCaseTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = TestDbContextFactory.Create(_connection);
        _context.Database.EnsureCreated();

        A.CallTo(() => _activitySource.GetCommitActivityAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<CommitActivity>(DataStatus.Available, null, null));
        A.CallTo(() => _activitySource.GetContributorsAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<Contributor>>(DataStatus.Available, [], null));
        A.CallTo(() => _activitySource.GetReleasesAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<ReleaseInfo>>(DataStatus.Available, [], null));
        A.CallTo(() => _collaborationSource.GetIssuesAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<IssueInfo>>(DataStatus.Available, [], null));
        A.CallTo(() => _collaborationSource.GetMergeRequestsAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<MergeRequestInfo>>(DataStatus.Available, [], null));
        A.CallTo(() => _securitySource.GetFindingsAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<SecurityFinding>>(DataStatus.Available, [], null));
        A.CallTo(() => _pipelineSource.GetPipelineRunsAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<PipelineRun>>(DataStatus.Available, [], null));
        A.CallTo(() => _codeHealthSource.GetCodeHealthAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<CodeHealthReport>(DataStatus.Available, null, null));
        A.CallTo(() => _documentationSource.GetDocumentationAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<DocumentationReport>(DataStatus.Available, null, null));

        A.CallTo(() => _healthCheckEngine.CheckAsync(A<RepositoryFacts>._, A<CancellationToken>._))
            .Invokes((RepositoryFacts facts, CancellationToken _) => _capturedFacts = facts)
            .Returns(new HealthCheckResult(null, DataStatus.Available, [], [], [], [], "test", Now));
        A.CallTo(() => _anomalyDetector.Detect(A<RepositoryFacts>._)).Returns([]);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenPrivateRepositoryHasNoOwner_DeniesAccess()
    {
        // Arrange
        var source = CreatePrivateRepository(ownerUserId: null);
        ArrangeCatalog(source);
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var action = () => systemUnderTests.AnalyzeAsync(new AnalyzeRepositoryRequest(source.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<RepositoryAccessDeniedException>();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenPrivateRepositoryHasDifferentOwner_DeniesAccess()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var source = CreatePrivateRepository(ownerId);
        ArrangeCatalog(source);
        SeedRepository(source, ownerId);
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var action = () => systemUnderTests.AnalyzeAsync(new AnalyzeRepositoryRequest(source.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<RepositoryAccessDeniedException>();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenPrivateRepositoryAndAnonymousUser_DeniesAccess()
    {
        // Arrange
        var source = CreatePrivateRepository(Guid.NewGuid());
        ArrangeCatalog(source);
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var action = () => systemUnderTests.AnalyzeAsync(new AnalyzeRepositoryRequest(source.Id, null), CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<RepositoryAccessDeniedException>();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenPrivateRepositoryOwnedByUser_GrantsAccess()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var source = CreatePrivateRepository(ownerId);
        ArrangeCatalog(source);
        SeedRepository(source, ownerId);
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        var actual = await systemUnderTests.AnalyzeAsync(new AnalyzeRepositoryRequest(source.Id, ownerId), CancellationToken.None);

        // Assert
        actual.AnalysisRunId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenCollaborationPartial_PropagatesPartialFlagsToFacts()
    {
        // Arrange
        var source = CreatePublicRepository();
        ArrangeCatalog(source);
        A.CallTo(() => _collaborationSource.GetIssuesAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<IssueInfo>>(DataStatus.Available, [], null, true));
        A.CallTo(() => _collaborationSource.GetMergeRequestsAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<MergeRequestInfo>>(DataStatus.Available, [], null, true));
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        await systemUnderTests.AnalyzeAsync(new AnalyzeRepositoryRequest(source.Id, null), CancellationToken.None);

        // Assert
        _capturedFacts.Should().NotBeNull();
        _capturedFacts!.IssuesPartial.Should().BeTrue();
        _capturedFacts.MergeRequestsPartial.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenHealthCheckReturnsCategoriesAndFindings_PersistsChildEntities()
    {
        // Arrange
        var source = CreatePublicRepository();
        ArrangeCatalog(source);
        var finding = new SecurityFinding("ext-1", SecurityFindingKind.Sast, SecuritySeverity.High, SecurityFindingStatus.Open, "SQL injection", "pkg", "src/file.cs", 7.5, 10, "sha-1");
        A.CallTo(() => _securitySource.GetFindingsAsync(A<string>._, A<CancellationToken>._))
            .Returns(new SourceCraftResult<IReadOnlyCollection<SecurityFinding>>(DataStatus.Available, [finding], null));
        var metric = new SourceCraftRepoHealthChecker.Application.HealthCheck.Models.MetricScore(MetricCode.SecurityHighFindings, 1, 50, 1, DataStatus.Available);
        var category = new CategoryScoreResult(ScoreCategory.Security, 50, 0.2, DataStatus.Available, [metric]);
        var recommendation = new RecommendationDraft(RecommendationPriority.High, "Исправить уязвимость", "Риск эксплуатации", "SAST: 1 High", "Обновите зависимость", 5, "Security:1");
        A.CallTo(() => _healthCheckEngine.CheckAsync(A<RepositoryFacts>._, A<CancellationToken>._))
            .Returns(new HealthCheckResult(50, DataStatus.Available, [category], [recommendation], [], [], "test", Now));
        var systemUnderTests = CreateSystemUnderTests();

        // Act
        await systemUnderTests.AnalyzeAsync(new AnalyzeRepositoryRequest(source.Id, null), CancellationToken.None);

        // Assert
        (await _context.CategoryScores.CountAsync()).Should().Be(1);
        (await _context.MetricScores.CountAsync()).Should().Be(1);
        (await _context.Recommendations.CountAsync()).Should().Be(1);
        (await _context.AnalysisFindings.CountAsync()).Should().Be(1);
    }

    private AnalyzeRepositoryUseCase CreateSystemUnderTests() => new(
        _context,
        _catalog,
        _activitySource,
        _collaborationSource,
        _securitySource,
        _pipelineSource,
        _codeHealthSource,
        _documentationSource,
        _healthCheckEngine,
        _anomalyDetector,
        Options.Create(new RepositoryOptions { MaxSourceCraftIdLength = 128, MaxNameLength = 256, MaxFullNameLength = 512, MaxUrlLength = 1024, MaxLanguageLength = 64 }),
        Options.Create(new RecommendationOptions { MaxTitleLength = 256, MaxProblemLength = 2048, MaxWhyImportantLength = 2048, MaxEvidenceLength = 2048, MaxActionLength = 2048, MaxExpectedImpactLength = 1024, MaxSourceReferenceLength = 512 }),
        Options.Create(new SecurityFindingOptions { MaxTitleLength = 512, MaxPackageLength = 256, MaxFilePathLength = 1024 }),
        TimeProvider.System,
        A.Dummy<ILogger<AnalyzeRepositoryUseCase>>());

    private void ArrangeCatalog(SourceCraftRepository source) =>
        A.CallTo(() => _catalog.GetRepositoryAsync(source.Id, A<CancellationToken>._))
            .Returns(new SourceCraftResult<SourceCraftRepository>(DataStatus.Available, source, null));

    private void SeedRepository(SourceCraftRepository source, Guid ownerId)
    {
        _context.Users.Add(new User
        {
            Id = ownerId,
            YaId = $"ya-{ownerId:N}",
            Login = $"owner-{ownerId:N}",
            DisplayName = "Owner",
            CreatedAt = Now.AddDays(-1)
        });
        _context.Repositories.Add(new Repository
        {
            Id = Guid.NewGuid(),
            SourceCraftId = source.Id,
            Name = source.Name,
            FullName = source.FullName,
            Url = source.Url,
            Language = source.Language,
            IsPrivate = source.IsPrivate,
            OwnerId = ownerId,
            CreatedAt = Now,
            LastActivityAt = Now
        });
        _context.SaveChanges();
    }

    private static SourceCraftRepository CreatePrivateRepository(Guid? ownerUserId) =>
        new("sc-1", "demo", "owner/demo", "https://sourcecraft.dev/owner/demo", "C#", 0, Now, true, "main", null, ownerUserId);

    private static SourceCraftRepository CreatePublicRepository() =>
        new("sc-1", "demo", "owner/demo", "https://sourcecraft.dev/owner/demo", "C#", 0, Now, false, "main");
}
