using System.Globalization;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.Services;

public sealed class AnomalyDetector(
    IOptions<AnomalyDetectionOptions> options,
    TimeProvider timeProvider) : IAnomalyDetector
{
    public IReadOnlyCollection<ActivityAnomaly> Detect(RepositoryFacts facts)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var anomalies = new List<ActivityAnomaly>();

        foreach (var contributor in facts.Contributors.Where(item => !item.IsBot && item.FirstCommitAt is not null && item.LastCommitAt is not null))
        {
            var first = contributor.FirstCommitAt!.Value;
            var last = contributor.LastCommitAt!.Value;
            var spanDays = (last - first).TotalDays;

            if (contributor.CommitsCount >= settings.CommitBurstThreshold
                && spanDays <= settings.CommitBurstWindowDays
                && (now - first).TotalDays <= settings.NewContributorRecentDays)
            {
                anomalies.Add(new ActivityAnomaly(
                    ActivityAnomalyKind.CommitBurst,
                    contributor.Login,
                    contributor.CommitsCount,
                    first,
                    last,
                    string.Create(CultureInfo.InvariantCulture, $"{contributor.CommitsCount} коммитов за {spanDays:0.#} дн. от ранее не коммитившего автора")));
            }

            if (contributor.CommitsCount <= settings.SmallChangeCommitThreshold
                && (now - last).TotalDays <= settings.SmallChangeRecentDays
                && (now - first).TotalDays >= settings.InactiveAuthorDays)
            {
                anomalies.Add(new ActivityAnomaly(
                    ActivityAnomalyKind.InactiveAuthorSmallChanges,
                    contributor.Login,
                    contributor.CommitsCount,
                    first,
                    last,
                    string.Create(CultureInfo.InvariantCulture, $"Ранее неактивный автор ({contributor.Login}) сделал {contributor.CommitsCount} небольших изменений")));
            }
        }

        foreach (var group in facts.MergeRequests.GroupBy(item => item.AuthorLogin))
        {
            var ordered = group.OrderBy(item => item.CreatedAt).ToArray();
            if (ordered.Length < settings.MergeRequestBurstThreshold)
                continue;

            var first = ordered[0].CreatedAt;
            var last = ordered[^1].CreatedAt;
            if ((last - first).TotalDays <= settings.MergeRequestBurstWindowDays && (now - first).TotalDays <= settings.NewContributorRecentDays)
            {
                anomalies.Add(new ActivityAnomaly(
                    ActivityAnomalyKind.MergeRequestBurst,
                    group.Key,
                    ordered.Length,
                    first,
                    last,
                    string.Create(CultureInfo.InvariantCulture, $"{ordered.Length} merge request за короткий промежуток от {group.Key}")));
            }
        }

        return anomalies;
    }
}
