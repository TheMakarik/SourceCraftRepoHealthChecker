using SourceCraftRepoHealthChecker.Application.Analysis;

namespace SourceCraftRepoHealthChecker.infrastructure.Analysis;

public sealed class AnalysisStatusHub(TimeProvider timeProvider) : IAnalysisStatusHub
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, AnalysisStatusEvent> _latest = [];
    private readonly List<Func<AnalysisStatusEvent, bool>> _subscribers = [];

    public void Publish(string repositoryId, string status, int? score)
    {
        var statusEvent = new AnalysisStatusEvent(repositoryId, status, score, timeProvider.GetUtcNow());

        List<Func<AnalysisStatusEvent, bool>> subscribers;
        lock (_lock)
        {
            _latest[repositoryId] = statusEvent;
            subscribers = [.. _subscribers];
        }

        foreach (var subscriber in subscribers)
        {
            if (!subscriber(statusEvent))
                RemoveSubscriber(subscriber);
        }
    }

    public IDisposable Subscribe(Func<AnalysisStatusEvent, bool> onEvent)
    {
        lock (_lock)
            _subscribers.Add(onEvent);

        return new AnalysisStatusSubscription(() => RemoveSubscriber(onEvent));
    }

    public IReadOnlyCollection<AnalysisStatusEvent> GetSnapshot()
    {
        lock (_lock)
            return [.. _latest.Values];
    }

    private void RemoveSubscriber(Func<AnalysisStatusEvent, bool> subscriber)
    {
        lock (_lock)
            _subscribers.Remove(subscriber);
    }
}
