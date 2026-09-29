using System.Net;
using Microsoft.Extensions.Options;
using Refit;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Interfaces;
using SourceCraftRepoHealthChecker.Application.SourceCraft.Models;
using SourceCraftRepoHealthChecker.Domain.Enums;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftSecurityAdapter(
    ISourceCraftApi sourceCraftApi,
    IAppSecApi appSecApi,
    IOptions<AppSecOptions> options) : ISourceCraftSecuritySource
{
    public async Task<SourceCraftResult<IReadOnlyCollection<SecurityFinding>>> GetFindingsAsync(string repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            var repository = await sourceCraftApi.GetRepositoryAsync(repositoryId, cancellationToken);
            if (string.IsNullOrEmpty(repository.Id))
                return new SourceCraftResult<IReadOnlyCollection<SecurityFinding>>(DataStatus.Unavailable, default, "SourceCraft repository has no AppSec gitRepo id");

            var settings = options.Value;
            var groups = await SourceCraftPagination.CollectAsync(
                0,
                settings.MaxPages,
                (pageToken, token) => FetchDefectGroupsAsync(repository.Id, pageToken, token),
                cancellationToken);

            var findings = groups.Select(MapFinding).ToArray();

            return new SourceCraftResult<IReadOnlyCollection<SecurityFinding>>(DataStatus.Available, findings, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return SourceCraftFailure.NoData<IReadOnlyCollection<SecurityFinding>>("AppSec has no data for this repository");
        }
        catch (Exception exception)
        {
            return SourceCraftFailure.Unavailable<IReadOnlyCollection<SecurityFinding>>(exception);
        }
    }

    private async Task<(IReadOnlyCollection<AppSecDefectGroupDto> Items, string? NextPageToken)> FetchDefectGroupsAsync(
        string gitRepository,
        string? pageToken,
        CancellationToken cancellationToken)
    {
        var page = await appSecApi.GetDefectGroupsAsync(gitRepository, options.Value.PageSize, pageToken, cancellationToken);
        return (page.Data ?? [], page.NextPageToken);
    }

    private static SecurityFinding MapFinding(AppSecDefectGroupDto defectGroup)
    {
        var id = string.IsNullOrEmpty(defectGroup.Uuid) ? defectGroup.PublicId?.Value ?? string.Empty : defectGroup.Uuid;
        var title = string.IsNullOrEmpty(defectGroup.RuleName) ? defectGroup.RuleId ?? string.Empty : defectGroup.RuleName;

        return new SecurityFinding(
            id,
            MapKind(defectGroup.EngineType, defectGroup.Engine),
            MapSeverity(defectGroup.Severity),
            MapStatus(defectGroup.Status),
            title,
            Optional(defectGroup.RuleId),
            Optional(defectGroup.FileName),
            defectGroup.CvssScore == 0 ? null : defectGroup.CvssScore,
            defectGroup.StartLine == 0 ? null : defectGroup.StartLine,
            Optional(defectGroup.LatestCommit));
    }

    private static SecurityFindingKind MapKind(int engineType, string? engine) =>
        MapEngineType((AppSecEngineType)engineType)
        ?? MapEngine(engine)
        ?? SecurityFindingKind.Sast;

    private static SecurityFindingKind? MapEngineType(AppSecEngineType engineType) => engineType switch
    {
        AppSecEngineType.Secrets => SecurityFindingKind.SecretScanning,
        AppSecEngineType.Sca => SecurityFindingKind.Sca,
        AppSecEngineType.Sast => SecurityFindingKind.Sast,
        _ => null
    };

    private static SecurityFindingKind? MapEngine(string? engine) => engine?.ToUpperInvariant() switch
    {
        "SECRETS" or "SECRET" or "SECRET_SCANNING" => SecurityFindingKind.SecretScanning,
        "SCA" => SecurityFindingKind.Sca,
        "SAST" => SecurityFindingKind.Sast,
        _ => null
    };

    private static SecuritySeverity MapSeverity(int severity) => severity switch
    {
        2 => SecuritySeverity.Medium,
        3 => SecuritySeverity.High,
        4 => SecuritySeverity.Critical,
        _ => SecuritySeverity.Low
    };

    private static SecurityFindingStatus MapStatus(int status) => status switch
    {
        0 => SecurityFindingStatus.Open,
        1 => SecurityFindingStatus.Fixed,
        2 => SecurityFindingStatus.Ignored,
        3 => SecurityFindingStatus.FalsePositive,
        _ => SecurityFindingStatus.Open
    };

    private static string? Optional(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
