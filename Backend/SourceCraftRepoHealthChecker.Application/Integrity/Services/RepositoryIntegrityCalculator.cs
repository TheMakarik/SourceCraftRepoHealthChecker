using System.Globalization;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Abstractions;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Models;
using SourceCraftRepoHealthChecker.Application.Integrity.Models;
using SourceCraftRepoHealthChecker.Application.Integrity.Options;

namespace SourceCraftRepoHealthChecker.Application.Integrity.Services;

public sealed class RepositoryIntegrityCalculator(
    IAnomalyDetector anomalyDetector,
    IOptions<RepositoryIntegrityOptions> options,
    TimeProvider timeProvider)
{
    public RepositoryIntegrity Calculate(RepositoryIntegrityInput input)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var signals = new List<(int Penalty, string Text)>();

        AddReusedActivityAnomalies(signals, input.Facts, settings);
        AddPersistedAnomalies(signals, input.PersistedAnomalySignals, settings);
        AddEmptyOrTinyCommits(signals, input.Facts, settings);
        AddLikesWithoutActivity(signals, input, now, settings);

        var penalty = signals.Sum(signal => signal.Penalty);
        var score = Math.Clamp(settings.MaximumScore - penalty, settings.MinimumScore, settings.MaximumScore);

        return new RepositoryIntegrity(
            ResolveStatus(score, settings),
            score,
            signals.Select(signal => signal.Text).ToArray());
    }

    private void AddReusedActivityAnomalies(List<(int Penalty, string Text)> signals, RepositoryFacts facts, RepositoryIntegrityOptions settings)
    {
        foreach (var anomaly in anomalyDetector.Detect(facts))
            AddSignal(signals, settings.ActivityAnomalyPenalty, anomaly.Description);
    }

    private static void AddPersistedAnomalies(List<(int Penalty, string Text)> signals, IReadOnlyCollection<string> persistedAnomalies, RepositoryIntegrityOptions settings)
    {
        foreach (var anomaly in persistedAnomalies)
        {
            if (string.IsNullOrWhiteSpace(anomaly))
                continue;

            AddSignal(signals, settings.ActivityAnomalyPenalty, anomaly);
        }
    }

    private static void AddEmptyOrTinyCommits(List<(int Penalty, string Text)> signals, RepositoryFacts facts, RepositoryIntegrityOptions settings)
    {
        if (facts.Commits is { TotalCount: 0 })
        {
            AddSignal(signals, settings.EmptyActivityPenalty, "В репозитории нет коммитов — история пуста или недоступна");
            return;
        }

        var contributors = facts.Contributors.Where(contributor => !contributor.IsBot).ToArray();
        if (contributors.Length < settings.TinyCommitMinimumContributors)
            return;

        var tinyContributors = contributors.Count(contributor => contributor.CommitsCount <= settings.TinyCommitCountThreshold);
        var tinyRatio = (double)tinyContributors / contributors.Length;
        if (tinyRatio >= settings.TinyCommitContributorRatio)
        {
            AddSignal(
                signals,
                settings.TinyCommitsPenalty,
                string.Create(CultureInfo.InvariantCulture, $"Большинство авторов ({tinyContributors} из {contributors.Length}) сделали ≤ {settings.TinyCommitCountThreshold} коммитов — активность выглядит точечной"));
        }
    }

    private static void AddLikesWithoutActivity(List<(int Penalty, string Text)> signals, RepositoryIntegrityInput input, DateTimeOffset now, RepositoryIntegrityOptions settings)
    {
        if (input.LikesCount < settings.SuspicionLikesMinimum)
            return;

        var inactivityDays = (now - input.LastActivityAt).TotalDays;
        if (inactivityDays >= settings.InactivityDays)
        {
            AddSignal(
                signals,
                settings.LikesWithoutActivityPenalty,
                string.Create(CultureInfo.InvariantCulture, $"Лайков: {input.LikesCount}, но активности нет {inactivityDays:0} дн. — возможна накрутка популярности"));
            return;
        }

        if (input.Facts.Commits is { TotalCount: 0 })
        {
            AddSignal(
                signals,
                settings.LikesWithoutActivityPenalty,
                string.Create(CultureInfo.InvariantCulture, $"Лайков: {input.LikesCount} при полном отсутствии коммитов — возможна накрутка популярности"));
        }
    }

    private static void AddSignal(List<(int Penalty, string Text)> signals, int penalty, string text)
    {
        if (signals.Any(signal => string.Equals(signal.Text, text, StringComparison.Ordinal)))
            return;

        signals.Add((penalty, text));
    }

    private static string ResolveStatus(int score, RepositoryIntegrityOptions settings)
    {
        if (score >= settings.OkMinimumScore)
            return RepositoryIntegrityStatuses.Ok;
        if (score <= settings.SuspiciousMaximumScore)
            return RepositoryIntegrityStatuses.Suspicious;

        return RepositoryIntegrityStatuses.Check;
    }
}
