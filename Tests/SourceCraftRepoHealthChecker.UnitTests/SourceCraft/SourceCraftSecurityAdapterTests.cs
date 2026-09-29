using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Options;
using SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

namespace SourceCraftRepoHealthChecker.UnitTests.SourceCraft;

public sealed class SourceCraftSecurityAdapterTests
{
    private readonly ISourceCraftApi _sourceCraftApi = A.Fake<ISourceCraftApi>();
    private readonly IAppSecApi _appSecApi = A.Fake<IAppSecApi>();
    private readonly SourceCraftSecurityAdapter systemUnderTests;

    public SourceCraftSecurityAdapterTests()
    {
        systemUnderTests = new SourceCraftSecurityAdapter(
            _sourceCraftApi,
            _appSecApi,
            Options.Create(new AppSecOptions
            {
                BaseUrl = "https://appsec.test",
                TimeoutSeconds = 1,
                MaxRetries = 0,
                PageSize = 100,
                MaxPages = 10
            }));

        A.CallTo(() => _sourceCraftApi.GetRepositoryAsync("repo-1", A<CancellationToken>._))
            .Returns(new RepositoryDto { Id = "git-1" });
    }

    [Theory]
    [InlineData(0, null, SecurityFindingKind.Sast)]
    [InlineData(1, null, SecurityFindingKind.SecretScanning)]
    [InlineData(2, null, SecurityFindingKind.Sca)]
    [InlineData(3, null, SecurityFindingKind.Sast)]
    [InlineData(0, "SECRETS", SecurityFindingKind.SecretScanning)]
    [InlineData(0, "SCA", SecurityFindingKind.Sca)]
    [InlineData(0, "SAST", SecurityFindingKind.Sast)]
    public async Task GetFindingsAsync_WhenEngineKnown_MapsFindingKind(int engineType, string? engine, SecurityFindingKind expected)
    {
        // Arrange
        ArrangePage(Group(engineType, engine));

        // Act
        var actual = await systemUnderTests.GetFindingsAsync("repo-1", CancellationToken.None);

        // Assert
        actual.Status.Should().Be(DataStatus.Available);
        actual.Data!.Single().Kind.Should().Be(expected);
    }

    [Fact]
    public async Task GetFindingsAsync_WhenNoFindings_ReturnsAvailableWithEmptyList()
    {
        // Arrange
        ArrangePage();

        // Act
        var actual = await systemUnderTests.GetFindingsAsync("repo-1", CancellationToken.None);

        // Assert
        actual.Status.Should().Be(DataStatus.Available);
        actual.Data.Should().BeEmpty();
    }

    private void ArrangePage(params AppSecDefectGroupDto[] groups) =>
        A.CallTo(() => _appSecApi.GetDefectGroupsAsync("git-1", A<int>._, A<string>._, A<CancellationToken>._))
            .Returns(new AppSecDefectGroupPageDto { Data = groups, NextPageToken = null });

    private static AppSecDefectGroupDto Group(int engineType, string? engine) => new()
    {
        Uuid = "finding-1",
        RuleName = "rule",
        EngineType = engineType,
        Engine = engine,
        Severity = 3,
        Status = 0
    };
}
