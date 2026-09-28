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
            if (findings.Length == 0)
                return SourceCraftFailure.NoData<IReadOnlyCollection<SecurityFinding>>("source returned no security findings");

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
            defectGroup.CvssScore == 0 ? null : defectGroup.CvssScore);
    }

    private static SecurityFindingKind MapKind(int engineType, string? engine)
    {
        switch (engine?.ToUpperInvariant())
        {
            case "SECRETS":
            case "SECRET":
            case "SECRET_SCANNING":
                return SecurityFindingKind.SecretScanning;
            case "SCA":
                return SecurityFindingKind.Sca;
        }

        return engineType switch
        {
            0 => SecurityFindingKind.SecretScanning,
            1 => SecurityFindingKind.Sca,
            _ => SecurityFindingKind.Sast
        };
    }

    private static SecuritySeverity MapSeverity(int severity) => severity switch
    {
        2 => SecuritySeverity.Medium,
        3 => SecuritySeverity.High,
        4 => SecuritySeverity.Critical,
        _ => SecuritySeverity.Low
    };

    private static SecurityFindingStatus MapStatus(int status) =>
        status == 0 ? SecurityFindingStatus.Open : SecurityFindingStatus.Fixed;

    private static string? Optional(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
