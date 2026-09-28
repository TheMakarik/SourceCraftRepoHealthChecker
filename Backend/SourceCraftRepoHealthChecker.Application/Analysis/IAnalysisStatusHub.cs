namespace SourceCraftRepoHealthChecker.Application.Analysis;

public interface IAnalysisStatusHub
{
    public void Publish(string repositoryId, string status, int? score);

    public IDisposable Subscribe(Func<AnalysisStatusEvent, bool> onEvent);

    public IReadOnlyCollection<AnalysisStatusEvent> GetSnapshot();
}
